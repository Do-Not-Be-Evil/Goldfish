namespace Goldfish.Domain.Pricing;

/// <summary>The winning rule for a policy, as returned by the matching query.</summary>
public sealed record PricingRuleMatch(
    Guid VersionId,
    int VersionNumber,
    Guid RuleId,
    string RuleName,
    int Priority,
    decimal Multiplier,
    bool WasCatchAll);
