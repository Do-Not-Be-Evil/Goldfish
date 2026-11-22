using Goldfish.Domain.RulesAdmin;

namespace Goldfish.Application.RulesAdmin;

/// <summary>The signed-in person, from Entra ID: object ID, display name and app roles.</summary>
public sealed record Actor(string Oid, string Name, bool IsAnalyst, bool IsManager)
{
    public const int OidMaxLength = 64;
    public const int NameMaxLength = 256;
}

public sealed record ChangeRequestSummary(
    Guid Id,
    string Reference,
    string Title,
    string CreatedByName,
    DateTime CreatedOnUtc,
    int BaseVersionNumber,
    ChangeRequestStatus Status,
    int? PublishedVersionNumber);

public sealed record RevisionView(
    Guid Id,
    int RevisionNumber,
    int BaseVersionNumber,
    string AuthorOid,
    string AuthorName,
    DateTime CreatedOnUtc,
    IReadOnlyList<DraftRule> Rules);

public sealed record EventView(
    long Id,
    ChangeRequestEventType Type,
    Guid? RevisionId,
    string ActorOid,
    string ActorName,
    string? Comment,
    DateTime OccurredOnUtc);

public sealed record ApprovalView(
    Guid RevisionId,
    string ApproverOid,
    string ApproverName,
    string? Comment,
    int PublishedVersionNumber,
    DateTime ApprovedOnUtc);

/// <summary>Everything stored for one change request. Revisions and events are in the order written.</summary>
public sealed record ChangeRequestData(
    Guid Id,
    string Reference,
    string Title,
    string CreatedByOid,
    string CreatedByName,
    DateTime CreatedOnUtc,
    IReadOnlyList<RevisionView> Revisions,
    IReadOnlyList<EventView> Events,
    ApprovalView? Approval)
{
    public RevisionView LatestRevision => Revisions[^1];

    public ChangeRequestStatus Status => ChangeRequestState.Derive(
        LatestRevision.Id,
        Events.Select(e => new StatusEvent(e.Type, e.RevisionId)),
        Approval is not null);

    /// <summary>
    /// Everyone who created, edited or submitted the request. None of them may approve or reject it;
    /// rulesadmin.ApproveAndPublish applies the same rule.
    /// </summary>
    public IReadOnlySet<string> Contributors =>
        Revisions.Select(r => r.AuthorOid)
            .Append(CreatedByOid)
            .Concat(Events.Where(e => e.Type == ChangeRequestEventType.Submitted).Select(e => e.ActorOid))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed record NewChangeRequest(
    Guid Id,
    string Reference,
    string Title,
    Actor CreatedBy,
    int BaseVersionNumber,
    IReadOnlyList<DraftRule> Rules);

/// <summary>What the signed-in person may do with a change request right now, with the reason when they may not.</summary>
public sealed record AvailableActions(
    bool CanEdit,
    bool CanRestart,
    bool CanSubmit,
    bool CanWithdraw,
    bool CanApprove,
    bool CanReject,
    bool CanComment,
    string? ReviewBlockedReason,
    string? SubmitBlockedReason);

public sealed record ChangeRequestDetails(
    ChangeRequestData Data,
    ChangeRequestStatus Status,
    int LatestVersionNumber,
    bool IsStale,
    RatioBand Band,
    IReadOnlyList<RuleChange> Diff,
    IReadOnlyList<RuleSetProblem> Problems,
    AvailableActions Actions);
