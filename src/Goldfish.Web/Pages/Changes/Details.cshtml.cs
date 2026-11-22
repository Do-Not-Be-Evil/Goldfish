using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Goldfish.Web.Pages.Changes;

/// <summary>
/// One change request: diff, history and the actions the signed-in person may take. Each action is a page
/// handler; the Application handlers re-check permissions, and approval is checked again in the database.
/// </summary>
public sealed class DetailsModel(
    GetChangeRequestHandler get,
    SubmitChangeRequestHandler submit,
    WithdrawChangeRequestHandler withdraw,
    RejectChangeRequestHandler reject,
    ApproveChangeRequestHandler approve,
    CommentOnChangeRequestHandler comment,
    RestartFromLatestHandler restart) : PageModel
{
    [FromRoute] public Guid Id { get; set; }

    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public bool Confirm { get; set; }
    [BindProperty] public Guid RevisionId { get; set; }

    [FromQuery(Name = "published")] public int? PublishedVersion { get; set; }

    public Actor Actor => WebRoles.ToActor(User);
    public ChangeRequestDetails Details { get; private set; } = default!;
    public string? Error { get; private set; }

    public sealed record HistoryItem(DateTime When, string Who, string What, string? Comment);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Page() : NotFound();

    public Task<IActionResult> OnPostSubmitAsync(CancellationToken ct) => RunAsync(submit.HandleAsync(Id, Actor, Comment, ct), ct);
    public Task<IActionResult> OnPostWithdrawAsync(CancellationToken ct) => RunAsync(withdraw.HandleAsync(Id, Actor, Comment, ct), ct);
    public Task<IActionResult> OnPostRejectAsync(CancellationToken ct) => RunAsync(reject.HandleAsync(Id, Actor, Comment, ct), ct);
    public Task<IActionResult> OnPostCommentAsync(CancellationToken ct) => RunAsync(comment.HandleAsync(Id, Actor, Comment, ct), ct);
    public Task<IActionResult> OnPostRestartAsync(CancellationToken ct) => RunAsync(restart.HandleAsync(Id, Actor, ct), ct);

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken cancellationToken)
    {
        var result = await approve.HandleAsync(Id, RevisionId, Actor, Confirm, Comment, cancellationToken);
        return result.IsSuccess
            ? RedirectToPage(new { id = Id, published = result.Value })
            : await FailAsync(result.Error.Description, cancellationToken);
    }

    private async Task<IActionResult> RunAsync(Task<Result> action, CancellationToken cancellationToken)
    {
        var result = await action;
        return result.IsSuccess ? RedirectToPage(new { id = Id }) : await FailAsync(result.Error.Description, cancellationToken);
    }

    private async Task<IActionResult> FailAsync(string message, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        Error = message;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await get.HandleAsync(Id, Actor, cancellationToken);
        if (result.IsFailure) return false;
        Details = result.Value;
        return true;
    }

    public IEnumerable<HistoryItem> History()
    {
        var data = Details.Data;
        var numbers = data.Revisions.ToDictionary(r => r.Id, r => r.RevisionNumber);
        var items = data.Revisions.Select(r => new HistoryItem(r.CreatedOnUtc, r.AuthorName,
            r.RevisionNumber == 1 ? $"started the request from version {r.BaseVersionNumber}"
                                  : $"saved revision {r.RevisionNumber} (based on version {r.BaseVersionNumber})", null)).ToList();

        foreach (var e in data.Events)
        {
            var rev = e.RevisionId is { } id && numbers.TryGetValue(id, out var n) ? $" revision {n}" : "";
            items.Add(new(e.OccurredOnUtc, e.ActorName, e.Type switch
            {
                ChangeRequestEventType.Submitted => $"submitted{rev} for approval",
                ChangeRequestEventType.Rejected => $"rejected{rev}",
                ChangeRequestEventType.Withdrawn => "withdrew the request",
                _ => "commented",
            }, e.Comment));
        }

        if (data.Approval is { } a)
        {
            items.Add(new(a.ApprovedOnUtc, a.ApproverName,
                $"approved revision {numbers[a.RevisionId]} and published it as version {a.PublishedVersionNumber}", a.Comment));
        }

        return items.OrderBy(i => i.When);
    }
}
