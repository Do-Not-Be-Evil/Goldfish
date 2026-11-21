using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Products;

namespace Goldfish.Application.Products.GetProductById;

public sealed record GetProductByIdQuery(Guid Id);

public sealed class GetProductByIdHandler(IProductRepository productRepository)
{
    public async Task<Result<ProductResponse>> HandleAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(query.Id, cancellationToken);

        return product is null
            ? Result.Failure<ProductResponse>(ProductErrors.NotFound)
            : Result.Success(ProductResponse.FromEntity(product));
    }
}
