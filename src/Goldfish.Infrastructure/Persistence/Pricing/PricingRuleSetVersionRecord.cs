namespace Goldfish.Infrastructure.Persistence.Pricing;

/// <summary>
/// Persistence-only mapping for pricing.PricingRuleSetVersions. Rows are written only by
/// pricing.PublishRuleSetVersion, so this is not a domain entity.
/// </summary>
internal sealed class PricingRuleSetVersionRecord
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeTicketRef { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime PublishedOnUtc { get; set; }
    public List<PricingRuleRecord> Rules { get; set; } = [];
}
