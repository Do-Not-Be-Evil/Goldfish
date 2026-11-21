using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Pricing;
using Goldfish.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Pricing;

internal sealed class SqlPricingStatusReader(ApplicationDbContext dbContext) : IPricingStatusReader
{
    public async Task<Result<ActiveVersionStatus>> GetAsync(CancellationToken cancellationToken = default)
    {
        var active = await dbContext.PricingRuleSetVersions
            .AsNoTracking()
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ActiveVersionStatus(v.VersionNumber, v.ChangeTicketRef, v.PublishedOnUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return active is null
            ? Result.Failure<ActiveVersionStatus>(PricingErrors.NoActiveVersion)
            : Result.Success(active);
    }
}
