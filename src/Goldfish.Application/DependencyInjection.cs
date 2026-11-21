using Goldfish.Application.Policies.CreatePolicy;
using Goldfish.Application.Policies.GetPolicyById;
using Goldfish.Application.Policies.UpdatePolicy;
using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.Application.Products.CreateProduct;
using Goldfish.Application.Products.GetProductById;
using Goldfish.Application.Quotes.CreateQuote;
using Goldfish.Application.Quotes.GetQuoteById;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Goldfish.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<CreateProductHandler>();
        services.AddScoped<GetProductByIdHandler>();

        services.AddScoped<CreatePolicyHandler>();
        services.AddScoped<UpdatePolicyHandler>();
        services.AddScoped<GetPolicyByIdHandler>();

        services.AddScoped<CreateQuoteHandler>();
        services.AddScoped<GetQuoteByIdHandler>();

        services.AddScoped<GetActiveVersionHandler>();

        return services;
    }
}
