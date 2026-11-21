using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;
using Goldfish.Domain.Quotes;
using Goldfish.Infrastructure.Persistence;
using Goldfish.Infrastructure.Pricing;
using Goldfish.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Goldfish.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPricingRuleReader, SqlPricingRuleReader>();
        services.AddScoped<IPricingStatusReader, SqlPricingStatusReader>();

        return services;
    }
}
