using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Products;

namespace Goldfish.Application.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price);

public sealed class CreateProductHandler(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<ProductResponse>> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = Product.Create(command.Name, command.Price, timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return Result.Failure<ProductResponse>(result.Error);
        }

        productRepository.Add(result.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProductResponse.FromEntity(result.Value));
    }
}
