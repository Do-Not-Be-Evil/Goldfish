using Goldfish.Application.Policies.CreatePolicy;
using Goldfish.Application.Policies.GetPolicyById;
using Goldfish.Application.Policies.ListPolicies;
using Goldfish.Application.Policies.UpdatePolicy;
using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.Application.Quotes.CreateQuote;
using Goldfish.Application.Quotes.GetQuoteById;
using Goldfish.Application.RulesAdmin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Goldfish.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<CreatePolicyHandler>();
        services.AddScoped<UpdatePolicyHandler>();
        services.AddScoped<GetPolicyByIdHandler>();
        services.AddScoped<ListPoliciesHandler>();

        services.AddScoped<CreateQuoteHandler>();
        services.AddScoped<GetQuoteByIdHandler>();

        services.AddScoped<GetActiveVersionHandler>();

        return services;
    }

    /// <summary>Handlers for the pricing rules admin app. Kept separate: the API host has no rules-admin store.</summary>
    public static IServiceCollection AddRulesAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<ListChangeRequestsHandler>();
        services.AddScoped<GetChangeRequestHandler>();
        services.AddScoped<CreateChangeRequestHandler>();
        services.AddScoped<SaveRevisionHandler>();
        services.AddScoped<RestartFromLatestHandler>();
        services.AddScoped<SubmitChangeRequestHandler>();
        services.AddScoped<WithdrawChangeRequestHandler>();
        services.AddScoped<RejectChangeRequestHandler>();
        services.AddScoped<CommentOnChangeRequestHandler>();
        services.AddScoped<ApproveChangeRequestHandler>();

        return services;
    }
}
