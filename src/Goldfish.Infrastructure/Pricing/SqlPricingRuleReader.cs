using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;
using Goldfish.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Pricing;

/// <summary>Picks the winning rule in SQL (decision 6). Rules are read on every call; nothing is cached.</summary>
internal sealed class SqlPricingRuleReader(ApplicationDbContext dbContext) : IPricingRuleReader
{
    public async Task<Result<PricingRuleMatch>> FindMatchAsync(
        PolicyType type,
        decimal coverageLimit,
        CancellationToken cancellationToken = default)
    {
        var policyType = type.ToString();

        // Interpolation holes become SQL parameters. The query starts with WITH, so EF must not compose over it.
        var rows = await dbContext.Database.SqlQuery<MatchRow>($"""
            WITH active AS (
                SELECT TOP (1) Id, VersionNumber
                FROM pricing.PricingRuleSetVersions
                ORDER BY VersionNumber DESC
            )
            SELECT a.Id AS VersionId, a.VersionNumber,
                   m.Id AS RuleId, m.Name, m.Priority, m.Multiplier, m.WasCatchAll
            FROM active a
            OUTER APPLY (
                SELECT TOP (1) r.Id, r.Name, r.Priority, r.Multiplier,
                       CAST(CASE WHEN r.PolicyType IS NULL
                                  AND r.MinCoverageLimitExclusive IS NULL
                                  AND r.MaxCoverageLimitInclusive IS NULL
                                 THEN 1 ELSE 0 END AS bit) AS WasCatchAll
                FROM pricing.PricingRules r
                WHERE r.VersionId = a.Id
                  AND (r.PolicyType IS NULL OR r.PolicyType = {policyType})
                  AND (r.MinCoverageLimitExclusive IS NULL OR {coverageLimit} > r.MinCoverageLimitExclusive)
                  AND (r.MaxCoverageLimitInclusive IS NULL OR {coverageLimit} <= r.MaxCoverageLimitInclusive)
                ORDER BY r.Priority ASC
            ) m
            """).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result.Failure<PricingRuleMatch>(PricingErrors.NoActiveVersion);
        }

        var row = rows[0];
        if (row.RuleId is null)
        {
            return Result.Failure<PricingRuleMatch>(PricingErrors.InvalidActiveVersion);
        }

        return Result.Success(new PricingRuleMatch(
            row.VersionId,
            row.VersionNumber,
            row.RuleId.Value,
            row.Name!,
            row.Priority!.Value,
            row.Multiplier!.Value,
            row.WasCatchAll!.Value));
    }

    private sealed record MatchRow(
        Guid VersionId,
        int VersionNumber,
        Guid? RuleId,
        string? Name,
        int? Priority,
        decimal? Multiplier,
        bool? WasCatchAll);
}
