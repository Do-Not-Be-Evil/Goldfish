using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Products;

public static class ProductErrors
{
    public static readonly Error NotFound =
        new("Product.NotFound", "The product with the specified identifier was not found.");

    public static readonly Error InvalidName =
        new("Product.InvalidName", $"Name is required and must be at most {Product.NameMaxLength} characters.");

    public static readonly Error InvalidPrice =
        new("Product.InvalidPrice", "Price cannot be negative.");
}
