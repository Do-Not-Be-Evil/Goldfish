namespace Goldfish.Domain.RulesAdmin;

public enum RuleChangeKind
{
    Unchanged,
    Changed,
    Added,
    Removed
}

/// <summary>One row of a field-level diff between a base version and a draft, matched by rule Name.</summary>
public sealed record RuleChange(
    string Name,
    RuleChangeKind Kind,
    DraftRule? Old,
    DraftRule? New,
    decimal? Ratio,
    IReadOnlyList<string> ChangedFields,
    bool NeedsReason)
{
    public bool HasReason => !string.IsNullOrWhiteSpace(New?.Reason);

    public bool MissingReason => NeedsReason && !HasReason;
}

public static class RuleSetDiff
{
    /// <summary>
    /// Compares a draft with its base version. A rule needs a reason when it is new, or when its
    /// multiplier ratio (new / old, rounded to 4 places like the publish dry run) is outside the band.
    /// Must match the reason check in rulesadmin.ApproveAndPublish.
    /// </summary>
    public static IReadOnlyList<RuleChange> Compute(
        IReadOnlyList<DraftRule> baseRules,
        IReadOnlyList<DraftRule> draftRules,
        RatioBand band)
    {
        var old = baseRules.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        var changes = new List<RuleChange>();

        foreach (var rule in draftRules)
        {
            if (!old.Remove(rule.Name, out var previous))
            {
                changes.Add(new RuleChange(rule.Name, RuleChangeKind.Added, null, rule, null, [], NeedsReason: true));
                continue;
            }

            var fields = ChangedFields(previous, rule);
            var ratio = Math.Round(rule.Multiplier / previous.Multiplier, 4, MidpointRounding.AwayFromZero);
            changes.Add(new RuleChange(
                rule.Name,
                fields.Count == 0 ? RuleChangeKind.Unchanged : RuleChangeKind.Changed,
                previous,
                rule,
                ratio,
                fields,
                NeedsReason: !band.Contains(ratio)));
        }

        changes.AddRange(old.Values.Select(r =>
            new RuleChange(r.Name, RuleChangeKind.Removed, r, null, null, [], NeedsReason: false)));

        return changes
            .OrderBy(c => (c.New ?? c.Old)!.Priority)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ChangedFields(DraftRule a, DraftRule b)
    {
        var fields = new List<string>();
        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) fields.Add(nameof(DraftRule.Name));
        if (a.Priority != b.Priority) fields.Add(nameof(DraftRule.Priority));
        if (!string.Equals(a.PolicyType, b.PolicyType, StringComparison.Ordinal)) fields.Add(nameof(DraftRule.PolicyType));
        if (a.MinCoverageLimitExclusive != b.MinCoverageLimitExclusive) fields.Add(nameof(DraftRule.MinCoverageLimitExclusive));
        if (a.MaxCoverageLimitInclusive != b.MaxCoverageLimitInclusive) fields.Add(nameof(DraftRule.MaxCoverageLimitInclusive));
        if (a.Multiplier != b.Multiplier) fields.Add(nameof(DraftRule.Multiplier));
        return fields;
    }
}
