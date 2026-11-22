using Goldfish.Domain.Policies;

namespace Goldfish.Domain.RulesAdmin;

/// <summary>A validation problem; <see cref="Row"/> is the zero-based rule index, or null for the whole set.</summary>
public sealed record RuleSetProblem(int? Row, string Field, string Message);

/// <summary>
/// Advisory checks that mirror pricing.PublishRuleSetVersion (errors 50002, 50004, 50005), the pricing.PricingRules
/// constraints and indexes, and the precision of pricing.PricingRuleInput. The procedure stays the final authority;
/// RuleSetValidatorParityTests keeps the two in step.
/// </summary>
public static class RuleSetValidator
{
    public const int NameMaxLength = 200;
    public const int ReasonMaxLength = 1000;
    public const decimal MultiplierMax = 99_999.9999m;
    public const decimal CoverageMax = 9_999_999_999_999_999.99m;

    public static IReadOnlyList<RuleSetProblem> Validate(IReadOnlyList<DraftRule> rules)
    {
        var problems = new List<RuleSetProblem>();

        if (rules.Count == 0)
        {
            problems.Add(new(null, "Rules", "The rule set is empty."));
            return problems;
        }

        for (var i = 0; i < rules.Count; i++)
        {
            ValidateRow(i, rules[i], problems);
        }

        // Rule names are unique under the database's default collation, which is case-insensitive.
        foreach (var group in rules.Select((r, i) => (r, i)).GroupBy(x => x.r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1 && g.Key.Length > 0))
        {
            foreach (var (_, i) in group.Skip(1))
                problems.Add(new(i, nameof(DraftRule.Name), $"Another rule is already named '{group.Key}'."));
        }

        foreach (var group in rules.Select((r, i) => (r, i)).GroupBy(x => x.r.Priority).Where(g => g.Count() > 1))
        {
            foreach (var (_, i) in group.Skip(1))
                problems.Add(new(i, nameof(DraftRule.Priority), $"Another rule already has priority {group.Key}."));
        }

        var catchAlls = rules.Select((r, i) => (r, i)).Where(x => x.r.IsCatchAll).ToList();
        if (catchAlls.Count != 1)
        {
            problems.Add(new(null, "Rules",
                "The rule set must contain exactly one catch-all rule (no policy type and no coverage limits)."));
        }
        else
        {
            var catchAll = catchAlls[0];
            if (rules.Any(r => !r.IsCatchAll && r.Priority >= catchAll.r.Priority))
            {
                problems.Add(new(catchAll.i, nameof(DraftRule.Priority),
                    "The catch-all rule must have a larger priority number than every other rule."));
            }
        }

        return problems;
    }

    private static void ValidateRow(int i, DraftRule rule, List<RuleSetProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            problems.Add(new(i, nameof(DraftRule.Name), "Name is required."));
        else if (rule.Name.Length > NameMaxLength)
            problems.Add(new(i, nameof(DraftRule.Name), $"Name must be {NameMaxLength} characters or fewer."));
        else if (rule.Name != rule.Name.Trim())
            problems.Add(new(i, nameof(DraftRule.Name), "Name must not start or end with spaces."));

        if (rule.Priority < 1)
            problems.Add(new(i, nameof(DraftRule.Priority), "Priority must be 1 or more."));

        if (rule.PolicyType is not null
            && rule.PolicyType is not (nameof(Policies.PolicyType.Commercial) or nameof(Policies.PolicyType.Personal)))
            problems.Add(new(i, nameof(DraftRule.PolicyType), $"Policy type must be one of: {string.Join(", ", Enum.GetNames<PolicyType>())}."));

        CheckCoverage(i, nameof(DraftRule.MinCoverageLimitExclusive), rule.MinCoverageLimitExclusive, problems);
        CheckCoverage(i, nameof(DraftRule.MaxCoverageLimitInclusive), rule.MaxCoverageLimitInclusive, problems);
        if (rule.MinCoverageLimitExclusive is { } min && rule.MaxCoverageLimitInclusive is { } max && min >= max)
            problems.Add(new(i, nameof(DraftRule.MaxCoverageLimitInclusive), "The maximum coverage must be larger than the minimum."));

        if (rule.Multiplier <= 0)
            problems.Add(new(i, nameof(DraftRule.Multiplier), "Multiplier must be greater than 0."));
        else if (rule.Multiplier > MultiplierMax)
            problems.Add(new(i, nameof(DraftRule.Multiplier), $"Multiplier must be {MultiplierMax} or less."));
        else if (decimal.Round(rule.Multiplier, 4) != rule.Multiplier)
            problems.Add(new(i, nameof(DraftRule.Multiplier), "Multiplier can have at most 4 decimal places."));

        if (rule.Reason is { Length: > ReasonMaxLength })
            problems.Add(new(i, nameof(DraftRule.Reason), $"Reason must be {ReasonMaxLength} characters or fewer."));
    }

    private static void CheckCoverage(int i, string field, decimal? value, List<RuleSetProblem> problems)
    {
        if (value is not { } v) return;
        if (v < 0 || v > CoverageMax)
            problems.Add(new(i, field, "Coverage limit is out of range."));
        else if (decimal.Round(v, 2) != v)
            problems.Add(new(i, field, "Coverage limits can have at most 2 decimal places."));
    }
}
