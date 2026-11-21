using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Domain.Pricing;

public interface IPricingRuleReader
{
    /// <summary>
    /// Finds the winning rule in the newest published version.
    /// Fails with <see cref="PricingErrors.NoActiveVersion"/> when no version has been published,
    /// and <see cref="PricingErrors.InvalidActiveVersion"/> when no rule matched.
    /// </summary>
    Task<Result<PricingRuleMatch>> FindMatchAsync(PolicyType type, decimal coverageLimit, CancellationToken cancellationToken = default);
}
