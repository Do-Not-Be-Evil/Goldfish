using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces Entra ID sign-in in the web apps under test. The user comes from the X-Test-User header,
/// "oid|name|role1,role2", and carries the raw Entra claim types the apps read: oid, name and roles.
/// </summary>
public static class TestAuthentication
{
    public const string UserHeader = "X-Test-User";
    public const string SchemeName = "Test";

    public static void AddTestAuthentication(this IServiceCollection services) =>
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = SchemeName;
                options.DefaultAuthenticateScheme = SchemeName;
                options.DefaultChallengeScheme = SchemeName;
                options.DefaultForbidScheme = SchemeName;
                options.DefaultSignInScheme = SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, Handler>(SchemeName, null);

    public static string HeaderValue(string oid, string name, params string[] roles) => $"{oid}|{name}|{string.Join(',', roles)}";

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var value))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = value.ToString().Split('|');
            var claims = new List<Claim> { new("oid", parts[0]), new("name", parts[1]) };
            claims.AddRange(parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim("roles", role)));

            var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
