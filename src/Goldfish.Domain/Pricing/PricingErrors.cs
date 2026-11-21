using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Pricing;

public static class PricingErrors
{
    public static readonly Error NoActiveVersion =
        Error.Unavailable("Pricing.NoActiveVersion", "No pricing rule version has been published.");

    public static readonly Error InvalidActiveVersion =
        Error.Failure("Pricing.InvalidActiveVersion",
            "The active pricing rule version has no rule matching this policy. Rule rows were written outside pricing.PublishRuleSetVersion.");
}
