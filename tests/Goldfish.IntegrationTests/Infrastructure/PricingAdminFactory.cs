using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Goldfish.Infrastructure.Persistence;
using Goldfish.PricingAdmin.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the pricing rules admin app against the test database. Entra ID sign-in is replaced by a test scheme
/// that reads the user from the X-Test-User header: "oid|name|role1,role2".
/// </summary>
public sealed class PricingAdminFactory(string connectionString) : WebApplicationFactory<Goldfish.PricingAdmin.Program>
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

        builder.ConfigureTestServices(services => services.AddTestAuthentication());
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

/// <summary>Posts Blazor static-SSR forms the way a browser does: antiforgery token plus the form's _handler name.</summary>
public static partial class FormPoster
{
    public static async Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client,
        string url,
        string formName,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken,
        string? pageUrl = null)
    {
        var page = await client.GetStringAsync(pageUrl ?? url, cancellationToken);
        var token = TokenPattern().Match(page);
        if (!token.Success)
        {
            throw new InvalidOperationException($"No antiforgery token on {url}.");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("_handler", formName),
            new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)),
        };
        form.AddRange(fields);

        return await client.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
    }

    // Blazor writes name then value; MVC and Razor Pages put type="hidden" in between.
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
