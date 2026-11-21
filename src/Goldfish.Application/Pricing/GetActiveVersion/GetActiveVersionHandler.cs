using Goldfish.Domain.Abstractions;

namespace Goldfish.Application.Pricing.GetActiveVersion;

public sealed class GetActiveVersionHandler(IPricingStatusReader statusReader)
{
    public Task<Result<ActiveVersionStatus>> HandleAsync(CancellationToken cancellationToken = default) =>
        statusReader.GetAsync(cancellationToken);
}
