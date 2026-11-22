using System.Security.Claims;
using Goldfish.Application.RulesAdmin;

namespace Goldfish.Web.Security;

/// <summary>
/// Entra ID app roles and the claim types the app reads. The two pricing roles match the Blazor admin app
/// (src/Goldfish.PricingAdmin); Policies.Editor is used only by this app.
/// </summary>
public static class WebRoles
{
    public const string Analyst = "Pricing.Analyst";
    public const string Manager = "Pricing.Manager";
    public const string PolicyEditor = "Policies.Editor";

    /// <summary>Change requests: analysts and managers.</summary>
    public const string PricingPolicy = "Pricing";

    /// <summary>Creating and editing policies.</summary>
    public const string PolicyEditPolicy = "PolicyEdit";

    public const string RoleClaimType = "roles";
    public const string NameClaimType = "name";
    public const string ObjectIdClaimType = "oid";
    private const string LongObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public static string ObjectId(ClaimsPrincipal user) =>
        user.FindFirstValue(ObjectIdClaimType) ?? user.FindFirstValue(LongObjectIdClaimType)
        ?? throw new InvalidOperationException("The signed-in user has no object ID (oid) claim.");

    /// <summary>The object ID is the identity used for "a contributor cannot approve"; the name is for display.</summary>
    public static Actor ToActor(ClaimsPrincipal user)
    {
        var oid = ObjectId(user);
        var name = user.FindFirstValue(NameClaimType) ?? user.FindFirstValue("preferred_username") ?? oid;

        return new Actor(
            Truncate(oid, Actor.OidMaxLength),
            Truncate(name, Actor.NameMaxLength),
            user.IsInRole(Analyst),
            user.IsInRole(Manager));
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
