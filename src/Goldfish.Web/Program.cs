using Goldfish.Application;
using Goldfish.Infrastructure;
using Goldfish.Web.Api;
using Goldfish.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Goldfish.Web;

/// <summary>
/// Goldfish web app: MVC controllers manage policies through Goldfish.Api, and Razor Pages manage pricing rule
/// change requests through the rules-admin handlers (publishing only via rulesadmin.ApproveAndPublish).
/// TypeScript in Scripts/ adds in-browser editing and live checks; every rule is enforced again on the server.
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

        builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = WebRoles.NameClaimType;
            options.TokenValidationParameters.RoleClaimType = WebRoles.RoleClaimType;
        });

        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(1);
            options.SlidingExpiration = false;
            options.AccessDeniedPath = "/AccessDenied";
        });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(WebRoles.PricingPolicy, p => p.RequireRole(WebRoles.Analyst, WebRoles.Manager))
            .AddPolicy(WebRoles.PolicyEditPolicy, p => p.RequireRole(WebRoles.PolicyEditor))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireRole(WebRoles.Analyst, WebRoles.Manager, WebRoles.PolicyEditor)
                .Build());

        builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Changes", WebRoles.PricingPolicy));

        builder.Services.AddOptions<GoldfishApiOptions>()
            .Bind(builder.Configuration.GetSection(GoldfishApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient<GoldfishApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GoldfishApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add(GoldfishApiClient.ApiKeyHeader, options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        builder.Services.AddRulesAdminApplication();
        builder.Services.AddRulesAdminInfrastructure(builder.Configuration);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets().AllowAnonymous();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
        app.MapRazorPages().WithStaticAssets();

        app.MapPost("/sign-out", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = "/" });
        });

        return app;
    }
}
