using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;

namespace Goldfish.Application.RulesAdmin;

/// <summary>
/// Storage for change requests. Every write is an insert: revisions, events and approvals are never updated
/// or deleted, so the stored rows are the audit trail.
/// </summary>
public interface IRulesAdminStore
{
    Task<IReadOnlyList<ChangeRequestSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<ChangeRequestData?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The newest published version number, or 0 when none has been published.</summary>
    Task<int> GetLatestVersionNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>The rules of a published version; empty for version 0.</summary>
    Task<IReadOnlyList<DraftRule>> GetVersionRulesAsync(int versionNumber, CancellationToken cancellationToken = default);

    Task<RatioBand> GetRatioBandAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(NewChangeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Adds a revision. Fails with <see cref="RulesAdminErrors.Concurrency"/> when that number already exists.</summary>
    Task<Result> AddRevisionAsync(
        Guid changeRequestId,
        int revisionNumber,
        int baseVersionNumber,
        Actor author,
        IReadOnlyList<DraftRule> rules,
        CancellationToken cancellationToken = default);

    Task AddEventAsync(
        Guid changeRequestId,
        Guid? revisionId,
        ChangeRequestEventType type,
        Actor actor,
        string? comment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls rulesadmin.ApproveAndPublish, which re-checks every approval rule and publishes the stored revision
    /// in the same transaction as the approval record. Returns the new version number.
    /// </summary>
    Task<Result<int>> ApproveAndPublishAsync(
        Guid changeRequestId,
        Guid revisionId,
        Actor approver,
        string? comment,
        CancellationToken cancellationToken = default);
}
