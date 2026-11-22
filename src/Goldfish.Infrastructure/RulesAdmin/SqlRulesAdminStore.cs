using System.Data;
using System.Data.Common;
using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;
using Goldfish.Infrastructure.Persistence;
using Goldfish.Infrastructure.Persistence.RulesAdmin;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.RulesAdmin;

internal sealed class SqlRulesAdminStore(ApplicationDbContext db) : IRulesAdminStore
{
    public async Task<IReadOnlyList<ChangeRequestSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var requests = await db.ChangeRequests.AsNoTracking().ToListAsync(cancellationToken);
        var revisions = await db.Revisions.AsNoTracking()
            .Select(r => new { r.Id, r.ChangeRequestId, r.RevisionNumber, r.BaseVersionNumber })
            .ToListAsync(cancellationToken);
        var events = await db.ChangeRequestEvents.AsNoTracking()
            .Where(e => e.Type != nameof(ChangeRequestEventType.Comment))
            .OrderBy(e => e.Id)
            .Select(e => new { e.ChangeRequestId, e.Type, e.RevisionId })
            .ToListAsync(cancellationToken);
        var approvals = await db.Approvals.AsNoTracking()
            .ToDictionaryAsync(a => a.ChangeRequestId, a => a.PublishedVersionNumber, cancellationToken);

        return requests
            .Select(c =>
            {
                var latest = revisions.Where(r => r.ChangeRequestId == c.Id).MaxBy(r => r.RevisionNumber)!;
                var status = ChangeRequestState.Derive(
                    latest.Id,
                    events.Where(e => e.ChangeRequestId == c.Id)
                        .Select(e => new StatusEvent(Enum.Parse<ChangeRequestEventType>(e.Type), e.RevisionId)),
                    approvals.ContainsKey(c.Id));
                return new ChangeRequestSummary(c.Id, c.Reference, c.Title, c.CreatedByName, c.CreatedOnUtc,
                    latest.BaseVersionNumber, status, approvals.TryGetValue(c.Id, out var v) ? v : null);
            })
            .OrderByDescending(s => s.CreatedOnUtc)
            .ToList();
    }

    public async Task<ChangeRequestData?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await db.ChangeRequests.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var revisions = await db.Revisions.AsNoTracking()
            .Include(r => r.Rules)
            .Where(r => r.ChangeRequestId == id)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync(cancellationToken);
        var events = await db.ChangeRequestEvents.AsNoTracking()
            .Where(e => e.ChangeRequestId == id)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
        var approval = await db.Approvals.AsNoTracking()
            .SingleOrDefaultAsync(a => a.ChangeRequestId == id, cancellationToken);

        return new ChangeRequestData(
            request.Id,
            request.Reference,
            request.Title,
            request.CreatedByOid,
            request.CreatedByName,
            request.CreatedOnUtc,
            revisions.Select(r => new RevisionView(r.Id, r.RevisionNumber, r.BaseVersionNumber, r.AuthorOid, r.AuthorName,
                r.CreatedOnUtc, r.Rules.OrderBy(x => x.Priority).Select(ToDraftRule).ToList())).ToList(),
            events.Select(e => new EventView(e.Id, Enum.Parse<ChangeRequestEventType>(e.Type), e.RevisionId, e.ActorOid,
                e.ActorName, e.Comment, e.OccurredOnUtc)).ToList(),
            approval is null
                ? null
                : new ApprovalView(approval.RevisionId, approval.ApproverOid, approval.ApproverName, approval.Comment,
                    approval.PublishedVersionNumber, approval.ApprovedOnUtc));
    }

    public async Task<int> GetLatestVersionNumberAsync(CancellationToken cancellationToken = default) =>
        await db.PricingRuleSetVersions.AsNoTracking().MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<DraftRule>> GetVersionRulesAsync(int versionNumber, CancellationToken cancellationToken = default)
    {
        if (versionNumber == 0)
        {
            return [];
        }

        return await db.PricingRules.AsNoTracking()
            .Where(r => db.PricingRuleSetVersions.Any(v => v.Id == r.VersionId && v.VersionNumber == versionNumber))
            .OrderBy(r => r.Priority)
            .Select(r => new DraftRule(r.Name, r.Priority, r.PolicyType, r.MinCoverageLimitExclusive,
                r.MaxCoverageLimitInclusive, r.Multiplier, null))
            .ToListAsync(cancellationToken);
    }

    public async Task<RatioBand> GetRatioBandAsync(CancellationToken cancellationToken = default)
    {
        var band = await db.RatioBands.AsNoTracking().SingleOrDefaultAsync(b => b.Id == 1, cancellationToken);
        return band is null ? RatioBand.Default : new RatioBand(band.MinRatio, band.MaxRatio);
    }

    public async Task CreateAsync(NewChangeRequest request, CancellationToken cancellationToken = default)
    {
        db.ChangeRequests.Add(new ChangeRequestRecord
        {
            Id = request.Id,
            Reference = request.Reference,
            Title = request.Title,
            CreatedByOid = request.CreatedBy.Oid,
            CreatedByName = request.CreatedBy.Name,
        });
        db.Revisions.Add(NewRevision(request.Id, 1, request.BaseVersionNumber, request.CreatedBy, request.Rules));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result> AddRevisionAsync(
        Guid changeRequestId,
        int revisionNumber,
        int baseVersionNumber,
        Actor author,
        IReadOnlyList<DraftRule> rules,
        CancellationToken cancellationToken = default)
    {
        db.Revisions.Add(NewRevision(changeRequestId, revisionNumber, baseVersionNumber, author, rules));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql
                                           && sql.Message.Contains("UX_Revisions_ChangeRequest_Number", StringComparison.Ordinal))
        {
            db.ChangeTracker.Clear();
            return Result.Failure(RulesAdminErrors.Concurrency);
        }
    }

    public async Task AddEventAsync(
        Guid changeRequestId,
        Guid? revisionId,
        ChangeRequestEventType type,
        Actor actor,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        db.ChangeRequestEvents.Add(new ChangeRequestEventRecord
        {
            ChangeRequestId = changeRequestId,
            RevisionId = revisionId,
            Type = type.ToString(),
            ActorOid = actor.Oid,
            ActorName = actor.Name,
            Comment = comment,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<int>> ApproveAndPublishAsync(
        Guid changeRequestId,
        Guid revisionId,
        Actor approver,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "rulesadmin.ApproveAndPublish";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30;
            command.Parameters.Add(new SqlParameter("@ChangeRequestId", SqlDbType.UniqueIdentifier) { Value = changeRequestId });
            command.Parameters.Add(new SqlParameter("@RevisionId", SqlDbType.UniqueIdentifier) { Value = revisionId });
            command.Parameters.Add(new SqlParameter("@ApproverOid", SqlDbType.NVarChar, Actor.OidMaxLength) { Value = approver.Oid });
            command.Parameters.Add(new SqlParameter("@ApproverName", SqlDbType.NVarChar, Actor.NameMaxLength) { Value = approver.Name });
            command.Parameters.Add(new SqlParameter("@Comment", SqlDbType.NVarChar, 2000) { Value = (object?)comment ?? DBNull.Value });
            var output = command.Parameters.Add(new SqlParameter("@NewVersionNumber", SqlDbType.Int) { Direction = ParameterDirection.Output });

            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Success((int)output.Value);
        }
        catch (SqlException ex) when (MapKnownError(ex) is { } error)
        {
            // A retry of an approval that already committed reports "already published": treat our own as success.
            if (error == RulesAdminErrors.AlreadyPublished && await FindOwnApprovalAsync(changeRequestId, revisionId, approver, cancellationToken) is { } version)
            {
                return Result.Success(version);
            }

            return Result.Failure<int>(error);
        }
        catch (DbException ex)
        {
            // The outcome is unknown (for example the connection dropped after COMMIT). Look before reporting.
            if (await FindOwnApprovalAsync(changeRequestId, revisionId, approver, CancellationToken.None) is { } version)
            {
                return Result.Success(version);
            }

            return Result.Failure<int>(RulesAdminErrors.PublishFailed((ex as SqlException)?.Number ?? 0, ex.Message));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<int?> FindOwnApprovalAsync(Guid changeRequestId, Guid revisionId, Actor approver, CancellationToken cancellationToken)
    {
        var approval = await db.Approvals.AsNoTracking()
            .SingleOrDefaultAsync(a => a.ChangeRequestId == changeRequestId, cancellationToken);
        return approval is not null && approval.RevisionId == revisionId && approval.ApproverOid == approver.Oid
            ? approval.PublishedVersionNumber
            : null;
    }

    /// <summary>Maps the wrapper's errors (51001-51007) and the publish procedure's to user-facing errors.</summary>
    private static Error? MapKnownError(SqlException ex) => ex.Number switch
    {
        51002 => RulesAdminErrors.NotFound,
        51003 => RulesAdminErrors.AlreadyPublished,
        51004 => RulesAdminErrors.Concurrency,
        51005 => RulesAdminErrors.WrongStatus,
        51006 => RulesAdminErrors.ContributorCannotApprove,
        51007 => RulesAdminErrors.MissingReason,
        50003 => RulesAdminErrors.Stale,
        2601 or 2627 when ex.Message.Contains("UX_PricingRuleSetVersions_VersionNumber", StringComparison.Ordinal) => RulesAdminErrors.Stale,
        2601 or 2627 when ex.Message.Contains("PK_Approvals", StringComparison.Ordinal) => RulesAdminErrors.AlreadyPublished,
        51001 or 50001 or 50002 or 50004 or 50005 or 547 or 2601 or 2627 => RulesAdminErrors.PublishFailed(ex.Number, ex.Message),
        _ => null
    };

    private static RevisionRecord NewRevision(Guid changeRequestId, int number, int baseVersion, Actor author, IReadOnlyList<DraftRule> rules) => new()
    {
        ChangeRequestId = changeRequestId,
        RevisionNumber = number,
        BaseVersionNumber = baseVersion,
        AuthorOid = author.Oid,
        AuthorName = author.Name,
        Rules = rules.Select(r => new RevisionRuleRecord
        {
            Priority = r.Priority,
            Name = r.Name,
            PolicyType = r.PolicyType,
            MinCoverageLimitExclusive = r.MinCoverageLimitExclusive,
            MaxCoverageLimitInclusive = r.MaxCoverageLimitInclusive,
            Multiplier = r.Multiplier,
            Reason = r.Reason,
        }).ToList(),
    };

    private static DraftRule ToDraftRule(RevisionRuleRecord r) =>
        new(r.Name, r.Priority, r.PolicyType, r.MinCoverageLimitExclusive, r.MaxCoverageLimitInclusive, r.Multiplier, r.Reason);
}
