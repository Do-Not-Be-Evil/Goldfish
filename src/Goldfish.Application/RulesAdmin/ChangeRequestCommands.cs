using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;
using Microsoft.Extensions.Logging;

namespace Goldfish.Application.RulesAdmin;

internal static class Limits
{
    public const int TitleMaxLength = 200;
    public const int CommentMaxLength = 2000;

    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static Error NotAllowed(string reason) => Error.Validation("RulesAdmin.NotAllowed", reason);

    /// <summary>Trims text and turns blank optional fields into nulls, so a stored revision has one spelling.</summary>
    public static IReadOnlyList<DraftRule> Normalize(IEnumerable<DraftRule> rules) =>
        rules.Select(r => r with
        {
            Name = r.Name?.Trim() ?? string.Empty,
            PolicyType = Clean(r.PolicyType),
            Reason = Clean(r.Reason),
        }).ToList();
}

/// <summary>Starts a change request as a copy of the live rule set.</summary>
public sealed class CreateChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result<Guid>> HandleAsync(Actor actor, string? title, CancellationToken cancellationToken = default)
    {
        if (!actor.IsAnalyst)
            return Result.Failure<Guid>(Limits.NotAllowed("Only analysts can start a change request."));

        var cleanTitle = Limits.Clean(title);
        if (cleanTitle is null || cleanTitle.Length > Limits.TitleMaxLength)
            return Result.Failure<Guid>(RulesAdminErrors.TitleRequired);

        var latest = await store.GetLatestVersionNumberAsync(cancellationToken);
        var rules = await store.GetVersionRulesAsync(latest, cancellationToken);
        var id = Guid.CreateVersion7();

        await store.CreateAsync(
            new NewChangeRequest(id, "GFA-" + id.ToString("N").ToUpperInvariant(), cleanTitle, actor, latest, rules),
            cancellationToken);

        return Result.Success(id);
    }
}

/// <summary>Saves the edited rules as a new revision on the same base version.</summary>
public sealed class SaveRevisionHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(
        Guid id,
        Actor actor,
        int expectedRevisionNumber,
        IReadOnlyList<DraftRule> rules,
        CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);
        if (!actor.IsAnalyst) return Result.Failure(Limits.NotAllowed("Only analysts can edit rules."));
        if (!ChangeRequestState.CanEdit(data.Status)) return Result.Failure(RulesAdminErrors.WrongStatus);
        if (data.LatestRevision.RevisionNumber != expectedRevisionNumber) return Result.Failure(RulesAdminErrors.Concurrency);

        var normalized = Limits.Normalize(rules);
        if (RuleSetValidator.Validate(normalized).Count > 0) return Result.Failure(RulesAdminErrors.Invalid);

        return await store.AddRevisionAsync(
            id, expectedRevisionNumber + 1, data.LatestRevision.BaseVersionNumber, actor, normalized, cancellationToken);
    }
}

/// <summary>
/// Replaces a stale draft's rules with the latest published rules, as a new revision. There is no merge:
/// the analyst re-enters their changes, which the history still shows.
/// </summary>
public sealed class RestartFromLatestHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(Guid id, Actor actor, CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);

        var details = await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken);
        if (!details.Actions.CanRestart)
            return Result.Failure(Limits.NotAllowed("Only an analyst can restart an editable draft that is out of date."));

        var rules = await store.GetVersionRulesAsync(details.LatestVersionNumber, cancellationToken);
        return await store.AddRevisionAsync(
            id, data.LatestRevision.RevisionNumber + 1, details.LatestVersionNumber, actor, rules, cancellationToken);
    }
}

public sealed class SubmitChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(Guid id, Actor actor, string? comment, CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);

        var details = await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken);
        if (!details.Actions.CanSubmit) return Result.Failure(Limits.NotAllowed(details.Actions.SubmitBlockedReason!));
        if (Limits.Clean(comment) is { Length: > Limits.CommentMaxLength }) return Result.Failure(RulesAdminErrors.CommentRequired);

        await store.AddEventAsync(id, data.LatestRevision.Id, ChangeRequestEventType.Submitted, actor, Limits.Clean(comment), cancellationToken);
        return Result.Success();
    }
}

public sealed class WithdrawChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(Guid id, Actor actor, string? comment, CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);

        var details = await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken);
        if (!details.Actions.CanWithdraw)
            return Result.Failure(Limits.NotAllowed("Only an analyst who worked on this request can withdraw it, before it is published."));
        if (Limits.Clean(comment) is { Length: > Limits.CommentMaxLength }) return Result.Failure(RulesAdminErrors.CommentRequired);

        await store.AddEventAsync(id, data.LatestRevision.Id, ChangeRequestEventType.Withdrawn, actor, Limits.Clean(comment), cancellationToken);
        return Result.Success();
    }
}

public sealed class RejectChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(Guid id, Actor actor, string? comment, CancellationToken cancellationToken = default)
    {
        var reason = Limits.Clean(comment);
        if (reason is null or { Length: > Limits.CommentMaxLength }) return Result.Failure(RulesAdminErrors.CommentRequired);

        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);

        var details = await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken);
        if (!details.Actions.CanReject) return Result.Failure(Limits.NotAllowed(details.Actions.ReviewBlockedReason!));

        await store.AddEventAsync(id, data.LatestRevision.Id, ChangeRequestEventType.Rejected, actor, reason, cancellationToken);
        return Result.Success();
    }
}

public sealed class CommentOnChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result> HandleAsync(Guid id, Actor actor, string? comment, CancellationToken cancellationToken = default)
    {
        var text = Limits.Clean(comment);
        if (text is null or { Length: > Limits.CommentMaxLength }) return Result.Failure(RulesAdminErrors.CommentRequired);
        if (!actor.IsAnalyst && !actor.IsManager) return Result.Failure(Limits.NotAllowed("Only analysts and managers can comment."));

        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure(RulesAdminErrors.NotFound);

        await store.AddEventAsync(id, data.LatestRevision.Id, ChangeRequestEventType.Comment, actor, text, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Approves the revision the manager reviewed and publishes it immediately. The checks here give friendly
/// messages; rulesadmin.ApproveAndPublish repeats them inside the publishing transaction.
/// </summary>
public sealed partial class ApproveChangeRequestHandler(IRulesAdminStore store, ILogger<ApproveChangeRequestHandler> logger)
{
    public async Task<Result<int>> HandleAsync(
        Guid id,
        Guid reviewedRevisionId,
        Actor actor,
        bool confirmedGoesLiveNow,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var result = await ApproveAsync(id, reviewedRevisionId, actor, confirmedGoesLiveNow, comment, cancellationToken);

        if (result.IsSuccess)
            LogPublished(id, reviewedRevisionId, result.Value, actor.Oid);
        else
            LogRefused(result.Error.Type == ErrorType.Failure ? LogLevel.Error : LogLevel.Warning,
                id, reviewedRevisionId, result.Error.Code, actor.Oid);

        return result;
    }

    private async Task<Result<int>> ApproveAsync(
        Guid id, Guid reviewedRevisionId, Actor actor, bool confirmed, string? comment, CancellationToken cancellationToken)
    {
        if (!confirmed) return Result.Failure<int>(RulesAdminErrors.ConfirmRequired);
        if (Limits.Clean(comment) is { Length: > Limits.CommentMaxLength }) return Result.Failure<int>(RulesAdminErrors.CommentRequired);

        var data = await store.GetAsync(id, cancellationToken);
        if (data is null) return Result.Failure<int>(RulesAdminErrors.NotFound);
        if (data.LatestRevision.Id != reviewedRevisionId) return Result.Failure<int>(RulesAdminErrors.Concurrency);

        var details = await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken);
        if (!details.Actions.CanApprove)
            return Result.Failure<int>(details.IsStale && details.Actions.CanReject
                ? RulesAdminErrors.Stale
                : Limits.NotAllowed(details.Actions.ReviewBlockedReason!));
        if (details.Diff.Any(c => c.MissingReason)) return Result.Failure<int>(RulesAdminErrors.MissingReason);

        return await store.ApproveAndPublishAsync(id, reviewedRevisionId, actor, Limits.Clean(comment), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Change request {ChangeRequestId} revision {RevisionId} published as version {VersionNumber}, approved by {ApproverOid}")]
    private partial void LogPublished(Guid changeRequestId, Guid revisionId, int versionNumber, string approverOid);

    [LoggerMessage(Message = "Approval of change request {ChangeRequestId} revision {RevisionId} refused with {ErrorCode} for {ApproverOid}")]
    private partial void LogRefused(LogLevel level, Guid changeRequestId, Guid revisionId, string errorCode, string approverOid);
}
