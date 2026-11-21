namespace Goldfish.Infrastructure.Persistence.Pricing;

/// <summary>Persistence-only mapping for pricing.PricingRules.</summary>
internal sealed class PricingRuleRecord
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public int Priority { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PolicyType { get; set; }
    public decimal? MinCoverageLimitExclusive { get; set; }
    public decimal? MaxCoverageLimitInclusive { get; set; }
    public decimal Multiplier { get; set; }
}
