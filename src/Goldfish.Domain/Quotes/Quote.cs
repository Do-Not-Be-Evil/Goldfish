using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;

namespace Goldfish.Domain.Quotes;

public sealed class Quote : Entity
{
    private Quote() { } // EF Core

    private Quote(Guid id) : base(id) { }

    public Guid PolicyId { get; private set; }
    public Guid VersionId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid RuleId { get; private set; }
    public string RuleName { get; private set; } = string.Empty;
    public bool WasCatchAll { get; private set; }
    public PolicyType PolicyType { get; private set; }
    public decimal CoverageLimit { get; private set; }
    public decimal BasePrice { get; private set; }
    public decimal Multiplier { get; private set; }
    public decimal UnroundedPrice { get; private set; }
    public decimal FinalPrice { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }

    public static Quote Create(Policy policy, PricingRuleMatch match, string requestedBy, DateTime utcNow)
    {
        var unrounded = policy.BasePrice * match.Multiplier;

        return new Quote(Guid.CreateVersion7())
        {
            PolicyId = policy.Id,
            VersionId = match.VersionId,
            VersionNumber = match.VersionNumber,
            RuleId = match.RuleId,
            RuleName = match.RuleName,
            WasCatchAll = match.WasCatchAll,
            PolicyType = policy.Type,
            CoverageLimit = policy.CoverageLimit,
            BasePrice = policy.BasePrice,
            Multiplier = match.Multiplier,
            UnroundedPrice = unrounded,
            FinalPrice = Math.Round(unrounded, 2, MidpointRounding.ToEven),
            RequestedBy = requestedBy,
            CreatedOnUtc = utcNow
        };
    }
}
