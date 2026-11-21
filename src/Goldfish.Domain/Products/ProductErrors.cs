using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Products;

public static class ProductErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Product.NotFound", "The product with the specified identifier was not found.");

    public static readonly Error InvalidName =
        Error.Validation("Product.InvalidName", $"Name is required and must be at most {Product.NameMaxLength} characters.");

    public static readonly Error InvalidPrice =
        Error.Validation("Product.InvalidPrice", "Price cannot be negative.");
}
