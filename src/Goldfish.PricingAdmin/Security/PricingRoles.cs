using System.Security.Claims;
using Goldfish.Application.RulesAdmin;

namespace Goldfish.PricingAdmin.Security;

/// <summary>Entra ID app roles defined on the app registration, and the claim types the app reads.</summary>
public static class PricingRoles
{
    public const string Analyst = "Pricing.Analyst";
    public const string Manager = "Pricing.Manager";

    public const string AnyRolePolicy = "AnyPricingRole";

    public const string RoleClaimType = "roles";
    public const string NameClaimType = "name";
    public const string ObjectIdClaimType = "oid";
    private const string LongObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>
    /// Builds the actor from the signed-in user. The object ID is the identity used for "a contributor cannot
    /// approve"; the name is for display and audit only.
    /// </summary>
    public static Actor ToActor(ClaimsPrincipal user)
    {
        var oid = user.FindFirstValue(ObjectIdClaimType) ?? user.FindFirstValue(LongObjectIdClaimType)
            ?? throw new InvalidOperationException("The signed-in user has no object ID (oid) claim.");
        var name = user.FindFirstValue(NameClaimType) ?? user.FindFirstValue("preferred_username") ?? oid;

        return new Actor(
            Truncate(oid, Actor.OidMaxLength),
            Truncate(name, Actor.NameMaxLength),
            user.IsInRole(Analyst),
            user.IsInRole(Manager));
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
