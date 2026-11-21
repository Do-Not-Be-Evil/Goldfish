using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;
using Goldfish.Domain.Quotes;
using Goldfish.Infrastructure.Persistence.Pricing;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Quote> Quotes => Set<Quote>();

    internal DbSet<PricingRuleSetVersionRecord> PricingRuleSetVersions => Set<PricingRuleSetVersionRecord>();
    internal DbSet<PricingRuleRecord> PricingRules => Set<PricingRuleRecord>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("The row was changed since it was read.", ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
