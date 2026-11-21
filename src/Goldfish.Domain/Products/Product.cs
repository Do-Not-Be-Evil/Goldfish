using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Products;

public sealed class Product : Entity
{
    public const int NameMaxLength = 200;

    private Product(Guid id, string name, decimal price, DateTime createdOnUtc)
        : base(id)
    {
        Name = name;
        Price = price;
        CreatedOnUtc = createdOnUtc;
    }

    private Product() { } // EF Core

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public static Result<Product> Create(string name, decimal price, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
        {
            return Result.Failure<Product>(ProductErrors.InvalidName);
        }

        if (price < 0)
        {
            return Result.Failure<Product>(ProductErrors.InvalidPrice);
        }

        return Result.Success(new Product(Guid.CreateVersion7(), name.Trim(), price, utcNow));
    }
}
