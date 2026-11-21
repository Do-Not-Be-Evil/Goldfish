using Goldfish.Domain.Abstractions;

namespace Goldfish.Application.Pricing.GetActiveVersion;

public interface IPricingStatusReader
{
    /// <summary>Returns the newest published version, or <c>Pricing.NoActiveVersion</c> when none exists.</summary>
    Task<Result<ActiveVersionStatus>> GetAsync(CancellationToken cancellationToken = default);
}
