namespace Goldfish.Domain.Quotes;

public interface IQuoteRepository
{
    /// <summary>Reads a quote without tracking.</summary>
    Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Quote quote);
}
