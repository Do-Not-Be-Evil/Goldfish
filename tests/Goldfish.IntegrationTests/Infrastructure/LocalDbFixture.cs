using Goldfish.Application;
using Goldfish.Infrastructure;
using Goldfish.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>
/// Creates an isolated LocalDB database per test run, applies the migrations, and drops it afterwards.
/// Runs as the developer's Windows login, which is sysadmin on LocalDB.
/// </summary>
public sealed class LocalDbFixture : IAsyncLifetime
{
    public const string DatabasePrefix = "Goldfish_Test_";

    public string DatabaseName { get; } = DatabasePrefix + Guid.NewGuid().ToString("N");

    public string ConnectionString =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    public ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>The real Application and Infrastructure registrations, pointed at the test database.</summary>
    public ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = ConnectionString })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
    }

    /// <summary>The rules admin app's registrations, pointed at the test database.</summary>
    public ServiceProvider CreateRulesAdminServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:RulesAdmin"] = ConnectionString })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddRulesAdminApplication()
            .AddRulesAdminInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
    }

    /// <summary>Empties every table this feature owns, in foreign-key order.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE rulesadmin.Approvals;
            DELETE rulesadmin.ChangeRequestEvents;
            DELETE rulesadmin.RevisionRules;
            DELETE rulesadmin.Revisions;
            DELETE rulesadmin.ChangeRequests;
            UPDATE rulesadmin.RatioBand SET MinRatio = 0.8, MaxRatio = 1.25;
            DELETE pricing.Quotes;
            DELETE pricing.PricingRules;
            DELETE pricing.PricingRuleSetVersions;
            ALTER TABLE dbo.Policies SET (SYSTEM_VERSIONING = OFF);
            DELETE dbo.Policies;
            EXEC (N'DELETE dbo.PoliciesHistory;'); -- compiled only after versioning is off
            ALTER TABLE dbo.Policies SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.PoliciesHistory));
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync(string table)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (int)(await command.ExecuteScalarAsync())!;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<LocalDbFixture>
{
    public const string Name = "Database";
}

/// <summary>Base class that resets the shared database before each test.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(LocalDbFixture database) : IAsyncLifetime
{
    protected LocalDbFixture Database { get; } = database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync() => await Database.ResetAsync();

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
