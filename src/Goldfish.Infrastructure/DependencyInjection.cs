using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.Application.RulesAdmin;
using Goldfish.Infrastructure.RulesAdmin;
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

    /// <summary>
    /// Registrations for the pricing rules admin app. It connects with its own login (connection string
    /// "RulesAdmin"), which can read the pricing tables, insert into the rulesadmin tables, and execute only
    /// rulesadmin.ApproveAndPublish. No retry strategy: an approval is never re-sent automatically, because
    /// its outcome after a dropped connection is checked by reading rulesadmin.Approvals instead.
    /// </summary>
    public static IServiceCollection AddRulesAdminInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RulesAdmin")
            ?? throw new InvalidOperationException("Connection string 'RulesAdmin' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IRulesAdminStore, SqlRulesAdminStore>();

        return services;
    }
}
