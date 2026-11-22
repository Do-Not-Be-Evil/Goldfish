using Goldfish.Domain.RulesAdmin;

namespace Goldfish.Application.RulesAdmin;

/// <summary>
/// Decides which actions a person may take. The handlers use it before writing, and the pages use it to show or
/// hide buttons. Approval is checked again in the database by rulesadmin.ApproveAndPublish.
/// </summary>
public static class ChangeRequestPolicy
{
    public static AvailableActions Evaluate(
        ChangeRequestData data,
        Actor actor,
        bool isStale,
        IReadOnlyList<RuleSetProblem> problems,
        IReadOnlyList<RuleChange> diff)
    {
        var status = data.Status;
        var isContributor = data.Contributors.Contains(actor.Oid);
        var editable = ChangeRequestState.CanEdit(status);

        string? submitBlocked =
            !actor.IsAnalyst ? "Only analysts can submit."
            : status != ChangeRequestStatus.Draft ? "Only a draft can be submitted. Save a new revision first."
            : isStale ? "A newer version has been published. Restart the draft from the latest version."
            : problems.Count > 0 ? "Fix the rule problems first."
            : diff.All(c => c.Kind == RuleChangeKind.Unchanged) ? "The draft makes no changes."
            : diff.Any(c => c.MissingReason) ? "Give a reason for every rule marked as needing one."
            : null;

        string? reviewBlocked =
            !actor.IsManager ? "Only managers can approve or reject."
            : status != ChangeRequestStatus.Submitted ? "Only a submitted change request can be approved or rejected."
            : isContributor ? RulesAdminErrors.ContributorCannotApprove.Description
            : null;

        return new AvailableActions(
            CanEdit: actor.IsAnalyst && editable,
            CanRestart: actor.IsAnalyst && editable && isStale,
            CanSubmit: submitBlocked is null,
            CanWithdraw: actor.IsAnalyst && isContributor && status is ChangeRequestStatus.Draft
                or ChangeRequestStatus.Submitted or ChangeRequestStatus.Rejected,
            CanApprove: reviewBlocked is null && !isStale,
            CanReject: reviewBlocked is null,
            CanComment: actor.IsAnalyst || actor.IsManager,
            ReviewBlockedReason: reviewBlocked ?? (isStale ? RulesAdminErrors.Stale.Description : null),
            SubmitBlockedReason: submitBlocked);
    }
}
