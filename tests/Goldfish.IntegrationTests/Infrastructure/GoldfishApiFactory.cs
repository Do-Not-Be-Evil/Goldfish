using System.Security.Cryptography;
using System.Text;
using Goldfish.Api.Authentication;
using Goldfish.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API against the test database. The connection string is set with UseSetting so it is in
/// place before AddInfrastructure reads it, and the host refuses to start against any other database.
/// </summary>
public sealed class GoldfishApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string FullKey = "full";
    public const string QuoterKey = "quoter";
    public const string ReaderKey = "reader";

    private static readonly Dictionary<string, (string Key, string[] Permissions)> TestKeys = new()
    {
        [FullKey] = (MakeKey(1), [.. Permissions.All]),
        [QuoterKey] = (MakeKey(2), [Permissions.QuotesCreate]),
        [ReaderKey] = (MakeKey(3), [Permissions.PoliciesRead, Permissions.QuotesRead, Permissions.PricingRead]),
    };

    /// <summary>A fixed 32-byte test key, base64url-encoded like production keys.</summary>
    private static string MakeKey(byte seed) =>
        Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(seed * 31 + i)).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public HttpClient CreateClient(string keyName)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, TestKeys[keyName].Key);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connectionString);

        var index = 0;
        foreach (var (name, (key, permissions)) in TestKeys)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            builder.UseSetting($"ApiKeys:Keys:{index}:Name", name);
            builder.UseSetting($"ApiKeys:Keys:{index}:KeyHash", hash);
            for (var p = 0; p < permissions.Length; p++)
            {
                builder.UseSetting($"ApiKeys:Keys:{index}:Permissions:{p}", permissions[p]);
            }
            index++;
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var databaseName = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetDbConnection().Database;
        if (!databaseName.StartsWith(LocalDbFixture.DatabasePrefix, StringComparison.Ordinal))
        {
            host.Dispose();
            throw new InvalidOperationException($"Refusing to run tests against database '{databaseName}'.");
        }

        return host;
    }
}

/// <summary>Base class for HTTP tests: resets the database and hosts the API against it.</summary>
public abstract class ApiTest(LocalDbFixture database) : DatabaseTest(database)
{
    private GoldfishApiFactory? _factory;

    protected GoldfishApiFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _factory = new GoldfishApiFactory(Database.ConnectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await base.DisposeAsync();
    }
}
