using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;
using Goldfish.Domain.Quotes;

namespace Goldfish.Domain.Tests.Quotes;

public sealed class QuoteTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Policy PolicyWithBasePrice(decimal basePrice) =>
        Policy.Create(PolicyType.Commercial, 2_000_000m, basePrice, "portal", Now).Value;

    private static PricingRuleMatch MatchWithMultiplier(decimal multiplier, bool catchAll = false) =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), "Large commercial", 10, multiplier, catchAll);

    [Fact]
    public void Cuj1_price_is_rounded_half_to_even()
    {
        var quote = Quote.Create(PolicyWithBasePrice(1_234.57m), MatchWithMultiplier(1.5m), "portal", Now);

        Assert.Equal(1_851.855m, quote.UnroundedPrice);
        Assert.Equal(1_851.86m, quote.FinalPrice);
    }

    [Fact]
    public void Midpoint_rounds_down_to_even()
    {
        var quote = Quote.Create(PolicyWithBasePrice(100.25m), MatchWithMultiplier(0.5m), "portal", Now);

        Assert.Equal(50.125m, quote.UnroundedPrice);
        Assert.Equal(50.12m, quote.FinalPrice);
    }

    [Fact]
    public void Midpoint_rounds_up_to_even()
    {
        var quote = Quote.Create(PolicyWithBasePrice(100.35m), MatchWithMultiplier(0.5m), "portal", Now);

        Assert.Equal(50.175m, quote.UnroundedPrice);
        Assert.Equal(50.18m, quote.FinalPrice);
    }

    [Fact]
    public void Snapshot_fields_are_copied_from_policy_and_match()
    {
        var policy = PolicyWithBasePrice(1_234.57m);
        var match = MatchWithMultiplier(1.0m, catchAll: true);

        var quote = Quote.Create(policy, match, "portal", Now);

        Assert.Equal(policy.Id, quote.PolicyId);
        Assert.Equal(match.VersionId, quote.VersionId);
        Assert.Equal(match.VersionNumber, quote.VersionNumber);
        Assert.Equal(match.RuleId, quote.RuleId);
        Assert.Equal(match.RuleName, quote.RuleName);
        Assert.True(quote.WasCatchAll);
        Assert.Equal(policy.Type, quote.PolicyType);
        Assert.Equal(policy.CoverageLimit, quote.CoverageLimit);
        Assert.Equal(policy.BasePrice, quote.BasePrice);
        Assert.Equal(match.Multiplier, quote.Multiplier);
        Assert.Equal("portal", quote.RequestedBy);
        Assert.Equal(Now, quote.CreatedOnUtc);
    }
}
