using Microsoft.AspNetCore.Authorization;

namespace Goldfish.Api.Authentication;

public static class Permissions
{
    public const string ClaimType = "permission";

    public const string PoliciesRead = "policies:read";
    public const string PoliciesWrite = "policies:write";
    public const string QuotesCreate = "quotes:create";
    public const string QuotesRead = "quotes:read";
    public const string PricingRead = "pricing:read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PoliciesRead, PoliciesWrite, QuotesCreate, QuotesRead, PricingRead
    };

    /// <summary>Registers one authorization policy per permission, named after the permission.</summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in All)
        {
            builder.AddPolicy(permission, policy => policy.RequireClaim(ClaimType, permission));
        }

        return builder;
    }
}
