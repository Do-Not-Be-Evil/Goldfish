using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Quotes;

namespace Goldfish.Application.Quotes.GetQuoteById;

public sealed record GetQuoteByIdQuery(Guid Id);

public sealed class GetQuoteByIdHandler(IQuoteRepository quoteRepository)
{
    public async Task<Result<QuoteResponse>> HandleAsync(
        GetQuoteByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var quote = await quoteRepository.GetByIdAsync(query.Id, cancellationToken);

        return quote is null
            ? Result.Failure<QuoteResponse>(QuoteErrors.NotFound)
            : Result.Success(QuoteResponse.FromEntity(quote));
    }
}
