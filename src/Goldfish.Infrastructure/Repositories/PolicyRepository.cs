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

    public async Task<(IReadOnlyList<Policy> Items, int Total)> ListAsync(
        int skip, int take, PolicyType? type, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Policies.AsNoTracking();
        if (type is { } t)
        {
            query = query.Where(p => p.Type == t);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedOnUtc)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(Policy policy) => dbContext.Policies.Add(policy);
}
