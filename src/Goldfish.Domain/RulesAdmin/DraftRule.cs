namespace Goldfish.Domain.RulesAdmin;

/// <summary>
/// One rule row in a change-request revision. Columns mirror pricing.PricingRuleInput; Reason is the
/// drafter's explanation, required when the rule is new or its multiplier moves outside the ratio band.
/// </summary>
public sealed record DraftRule(
    string Name,
    int Priority,
    string? PolicyType,
    decimal? MinCoverageLimitExclusive,
    decimal? MaxCoverageLimitInclusive,
    decimal Multiplier,
    string? Reason = null)
{
    public bool IsCatchAll =>
        PolicyType is null && MinCoverageLimitExclusive is null && MaxCoverageLimitInclusive is null;
}
