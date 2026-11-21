using Goldfish.Domain.Policies;
using Goldfish.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Repositories;

internal sealed class PolicyRepository(ApplicationDbContext dbContext) : IPolicyRepository
{
    public Task<Policy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Policy?> GetForUpdateAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.Policies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is not null)
        {
            // The UPDATE's WHERE clause compares against the original value, so a stale If-Match fails the save.
            dbContext.Entry(policy).Property(p => p.RowVersion).OriginalValue = expectedRowVersion;
        }

        return policy;
    }

    public void Add(Policy policy) => dbContext.Policies.Add(policy);
}
