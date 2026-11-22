using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.RulesAdmin;
using Goldfish.Web.Api;
using Goldfish.Web.Models;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Goldfish.Web.Controllers;

public sealed class HomeController(GoldfishApiClient api, ListChangeRequestsHandler changes) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var active = await api.GetActiveVersionAsync(cancellationToken);

        var open = 0;
        var awaiting = 0;
        if (User.IsInRole(WebRoles.Analyst) || User.IsInRole(WebRoles.Manager))
        {
            var list = await changes.HandleAsync(cancellationToken);
            open = list.Count(c => c.Status is ChangeRequestStatus.Draft or ChangeRequestStatus.Rejected);
            awaiting = list.Count(c => c.Status == ChangeRequestStatus.Submitted);
        }

        return View(new HomeViewModel(
            active.Value,
            active.IsSuccess ? null : active.Problem!.Detail ?? active.Problem.Title,
            open,
            awaiting));
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
