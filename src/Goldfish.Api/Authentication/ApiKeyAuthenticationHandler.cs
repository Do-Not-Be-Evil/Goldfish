using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Goldfish.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<ApiKeyOptions> apiKeys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrEmpty(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(values.ToString()));

        // Compare against every entry so the time taken does not reveal which key matched.
        ApiKeyEntry? match = null;
        foreach (var entry in apiKeys.CurrentValue.Keys)
        {
            if (CryptographicOperations.FixedTimeEquals(presentedHash, Convert.FromHexString(entry.KeyHash)))
            {
                match = entry;
            }
        }

        if (match is null)
        {
            // The key value is never logged.
            return Task.FromResult(AuthenticateResult.Fail("Unknown API key."));
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, match.Name) };
        claims.AddRange(match.Permissions.Select(p => new Claim(Permissions.ClaimType, p)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
