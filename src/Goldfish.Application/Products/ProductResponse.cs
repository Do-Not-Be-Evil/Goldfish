using Goldfish.Domain.Products;

namespace Goldfish.Application.Products;

public sealed record ProductResponse(Guid Id, string Name, decimal Price, DateTime CreatedOnUtc)
{
    internal static ProductResponse FromEntity(Product product) =>
        new(product.Id, product.Name, product.Price, product.CreatedOnUtc);
}
