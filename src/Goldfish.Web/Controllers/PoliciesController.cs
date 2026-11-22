using System.Net;
using Goldfish.Web.Api;
using Goldfish.Web.Models;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Web.Controllers;

/// <summary>
/// Lists, creates and edits policies through Goldfish.Api. Every signed-in role can read; only Policies.Editor
/// can change. Edits carry the policy's ETag, so the API refuses an edit based on stale values (412).
/// </summary>
public sealed partial class PoliciesController(GoldfishApiClient api, ILogger<PoliciesController> logger) : Controller
{
    private const int PageSize = 25;

    private bool CanEdit => User.IsInRole(WebRoles.PolicyEditor);

    public async Task<IActionResult> Index(int page = 1, string? type = null, CancellationToken cancellationToken = default)
    {
        type = PolicyFormModel.PolicyTypes.Contains(type, StringComparer.Ordinal) ? type : null;
        var result = await api.ListPoliciesAsync((Math.Max(page, 1) - 1) * PageSize, PageSize, type, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiFailure(result.Problem!);
        }

        return View(new PolicyListViewModel(result.Value!, type, CanEdit));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetPolicyAsync(id, cancellationToken);
        if (result.Problem.Is(HttpStatusCode.NotFound)) return NotFound();
        if (!result.IsSuccess) return ApiFailure(result.Problem!);

        ViewBag.CanEdit = CanEdit;
        return View(result.Value);
    }

    [Authorize(Policy = WebRoles.PolicyEditPolicy)]
    public IActionResult Create() => View(new PolicyFormModel());

    [HttpPost, Authorize(Policy = WebRoles.PolicyEditPolicy)]
    public async Task<IActionResult> Create(PolicyFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(form);

        var result = await api.CreatePolicyAsync(form.ToInput(), cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result.Problem!);
            return View(form);
        }

        LogChanged("created", result.Value!.Id, WebRoles.ObjectId(User), result.Value.BasePrice);
        TempData["Notice"] = "Policy created.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [Authorize(Policy = WebRoles.PolicyEditPolicy)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetPolicyAsync(id, cancellationToken);
        if (result.Problem.Is(HttpStatusCode.NotFound)) return NotFound();
        if (!result.IsSuccess) return ApiFailure(result.Problem!);

        return View(new PolicyEditViewModel(id, PolicyFormModel.From(result.Value!), result.Value, null));
    }

    [HttpPost, Authorize(Policy = WebRoles.PolicyEditPolicy)]
    public async Task<IActionResult> Edit(Guid id, PolicyFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(form.ETag))
        {
            if (string.IsNullOrEmpty(form.ETag)) ModelState.AddModelError(string.Empty, "Reload the policy before editing it.");
            return View(new PolicyEditViewModel(id, form, null, null));
        }

        var result = await api.UpdatePolicyAsync(id, form.ETag, form.ToInput(), cancellationToken);
        if (result.Problem.Is(HttpStatusCode.NotFound)) return NotFound();

        if (result.Problem.Is(HttpStatusCode.PreconditionFailed))
        {
            // Someone changed the policy since this edit started. Keep the typed values, show the current ones,
            // and adopt the new ETag so saving again is a deliberate overwrite of what is shown.
            var current = await api.GetPolicyAsync(id, cancellationToken);
            if (!current.IsSuccess) return ApiFailure(current.Problem!);

            ModelState.Clear();
            ModelState.AddModelError(string.Empty,
                $"This policy was changed by {current.Value!.ModifiedBy} while you were editing. Compare the current values below, then save again to replace them.");
            form.ETag = current.Value.ETag;
            return View(new PolicyEditViewModel(id, form, null, current.Value));
        }

        if (!result.IsSuccess)
        {
            AddApiError(result.Problem!);
            return View(new PolicyEditViewModel(id, form, null, null));
        }

        LogChanged("updated", id, WebRoles.ObjectId(User), result.Value!.BasePrice);
        TempData["Notice"] = "Policy saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private void AddApiError(ApiProblem problem)
    {
        var field = problem.Title switch
        {
            "Policy.InvalidType" => nameof(PolicyFormModel.PolicyType),
            "Policy.InvalidCoverageLimit" => nameof(PolicyFormModel.CoverageLimit),
            "Policy.InvalidBasePrice" => nameof(PolicyFormModel.BasePrice),
            _ => string.Empty,
        };
        ModelState.AddModelError(field, problem.Detail ?? problem.Title ?? "The API refused the change.");
    }

    private IActionResult ApiFailure(ApiProblem problem)
    {
        LogApiFailure(problem.Status, problem.Title);
        return View("ApiError", problem);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Policy {PolicyId} {Action} by {UserOid}; base price {BasePrice}")]
    private partial void LogChanged(string action, Guid policyId, string userOid, decimal basePrice);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Goldfish API returned {Status} {Title}")]
    private partial void LogApiFailure(int status, string? title);
}
