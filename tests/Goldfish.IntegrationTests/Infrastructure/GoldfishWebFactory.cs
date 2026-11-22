using Goldfish.Web.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts Goldfish.Web against the test database, with its API client sent to an in-memory Goldfish.Api
/// (<paramref name="api"/>) using the full-permission test key. Entra ID is replaced by <see cref="TestAuthentication"/>.
/// </summary>
public sealed class GoldfishWebFactory(string connectionString, GoldfishApiFactory api) : WebApplicationFactory<Goldfish.Web.Program>
{
    public HttpClient CreateClient(string oid, string name, params string[] roles)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthentication.UserHeader, TestAuthentication.HeaderValue(oid, name, roles));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RulesAdmin", connectionString);
        builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000001");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000002");
        builder.UseSetting("GoldfishApi:BaseUrl", "http://goldfish-api.test/");
        builder.UseSetting("GoldfishApi:ApiKey", GoldfishApiFactory.KeyFor(GoldfishApiFactory.FullKey));

        builder.ConfigureTestServices(services =>
        {
            services.AddTestAuthentication();
            services.AddHttpClient<GoldfishApiClient>().ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
        });
    }
}
