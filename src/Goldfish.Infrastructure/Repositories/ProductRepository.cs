using Goldfish.Domain.Products;
using Goldfish.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Repositories;

internal sealed class ProductRepository(ApplicationDbContext dbContext) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(Product product) => dbContext.Products.Add(product);
}
