using Goldfish.Application;
using Goldfish.Infrastructure;
using Goldfish.PricingAdmin.Components;
using Goldfish.PricingAdmin.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Goldfish.PricingAdmin;

/// <summary>
/// The pricing rules admin web app: analysts draft rule-set changes, managers approve them, and approval publishes
/// through rulesadmin.ApproveAndPublish. A separate host from Goldfish.Api so the API's API-key authentication and
/// its 401 responses are unchanged.
/// </summary>
public class Program
{
    public static void Main(string[] args) => Build(args).Run();

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

        // Read the raw Entra claims: "oid", "name" and "roles".
        builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = PricingRoles.NameClaimType;
            options.TokenValidationParameters.RoleClaimType = PricingRoles.RoleClaimType;
        });

        // Roles are read from the sign-in cookie, so a removed role takes effect within this lifetime.
        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(1);
            options.SlidingExpiration = false;
            options.AccessDeniedPath = "/access-denied";
        });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PricingRoles.AnyRolePolicy, p => p.RequireRole(PricingRoles.Analyst, PricingRoles.Manager))
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireRole(PricingRoles.Analyst, PricingRoles.Manager)
                .Build());

        builder.Services.AddRazorComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddRulesAdminApplication();
        builder.Services.AddRulesAdminInfrastructure(builder.Configuration);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapStaticAssets().AllowAnonymous();
        app.MapRazorComponents<App>();

        app.MapPost("/sign-out", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/" });
        });

        return app;
    }
}
