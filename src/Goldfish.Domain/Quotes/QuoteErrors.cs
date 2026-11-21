using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Quotes;

public static class QuoteErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Quote.NotFound", "The quote with the specified identifier was not found.");
}
