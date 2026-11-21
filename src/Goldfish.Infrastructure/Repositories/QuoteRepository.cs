using Goldfish.Domain.Quotes;
using Goldfish.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.Infrastructure.Repositories;

internal sealed class QuoteRepository(ApplicationDbContext dbContext) : IQuoteRepository
{
    public Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public void Add(Quote quote) => dbContext.Quotes.Add(quote);
}
