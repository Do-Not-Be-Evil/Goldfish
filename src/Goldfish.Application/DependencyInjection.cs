using Goldfish.Application.Products.CreateProduct;
using Goldfish.Application.Products.GetProductById;
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

        return services;
    }
}
