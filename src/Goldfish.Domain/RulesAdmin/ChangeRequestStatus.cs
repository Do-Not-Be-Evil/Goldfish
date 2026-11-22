namespace Goldfish.Domain.RulesAdmin;

public enum ChangeRequestStatus
{
    Draft,
    Submitted,
    Rejected,
    Withdrawn,
    Published
}

/// <summary>Event types stored in rulesadmin.ChangeRequestEvents. Approvals have their own table.</summary>
public enum ChangeRequestEventType
{
    Submitted,
    Rejected,
    Withdrawn,
    Comment
}

/// <summary>One decision-relevant event: its type and the revision it applied to.</summary>
public sealed record StatusEvent(ChangeRequestEventType Type, Guid? RevisionId);

public static class ChangeRequestState
{
    /// <summary>
    /// Derives the status from the stored, append-only history. Must match the "latest event is Submitted
    /// for the latest revision" check in rulesadmin.ApproveAndPublish.
    /// </summary>
    /// <param name="latestRevisionId">The highest-numbered revision.</param>
    /// <param name="events">Events in the order they were written.</param>
    /// <param name="isApproved">Whether a row exists in rulesadmin.Approvals.</param>
    public static ChangeRequestStatus Derive(Guid latestRevisionId, IEnumerable<StatusEvent> events, bool isApproved)
    {
        if (isApproved)
        {
            return ChangeRequestStatus.Published;
        }

        var last = events.LastOrDefault(e => e.Type != ChangeRequestEventType.Comment);
        return last?.Type switch
        {
            ChangeRequestEventType.Withdrawn => ChangeRequestStatus.Withdrawn,
            ChangeRequestEventType.Submitted when last.RevisionId == latestRevisionId => ChangeRequestStatus.Submitted,
            ChangeRequestEventType.Rejected when last.RevisionId == latestRevisionId => ChangeRequestStatus.Rejected,
            _ => ChangeRequestStatus.Draft
        };
    }

    public static bool CanEdit(ChangeRequestStatus status) =>
        status is ChangeRequestStatus.Draft or ChangeRequestStatus.Rejected;
}
