using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.RulesAdmin;

public static class RulesAdminErrors
{
    public static readonly Error NotFound =
        Error.NotFound("RulesAdmin.NotFound", "The change request was not found.");

    public static readonly Error Forbidden =
        Error.Validation("RulesAdmin.Forbidden", "Your role does not allow this action.");

    public static readonly Error WrongStatus =
        Error.Validation("RulesAdmin.WrongStatus", "The change request is not in a state that allows this action.");

    public static readonly Error Invalid =
        Error.Validation("RulesAdmin.Invalid", "The rule set has problems. Fix the highlighted fields.");

    public static readonly Error MissingReason =
        Error.Validation("RulesAdmin.MissingReason",
            "Every new rule, and every rule whose multiplier moves outside the ratio band, needs a reason.");

    public static readonly Error TitleRequired =
        Error.Validation("RulesAdmin.TitleRequired", "A title of 1-200 characters is required.");

    public static readonly Error CommentRequired =
        Error.Validation("RulesAdmin.CommentRequired", "A comment of 1-2000 characters is required.");

    public static readonly Error ConfirmRequired =
        Error.Validation("RulesAdmin.ConfirmRequired", "Confirm that approving publishes the change immediately.");

    public static readonly Error Stale =
        Error.PreconditionFailed("RulesAdmin.Stale",
            "A newer rule version has been published since this draft was started. Restart the draft from the latest version.");

    public static readonly Error Concurrency =
        Error.PreconditionFailed("RulesAdmin.Concurrency",
            "Someone else changed this change request while you were working. Reload and try again.");

    public static readonly Error ContributorCannotApprove =
        Error.Validation("RulesAdmin.ContributorCannotApprove",
            "You drafted, edited or submitted this change request, so you cannot approve or reject it.");

    public static readonly Error AlreadyPublished =
        Error.PreconditionFailed("RulesAdmin.AlreadyPublished", "This change request has already been published.");

    public static Error PublishFailed(int number, string message) =>
        Error.Failure("RulesAdmin.PublishFailed", $"Publishing failed (SQL error {number}): {message}");
}
