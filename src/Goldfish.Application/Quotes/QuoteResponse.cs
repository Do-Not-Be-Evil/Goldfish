using Goldfish.Domain.Quotes;

namespace Goldfish.Application.Quotes;

public sealed record QuoteResponse(
    Guid Id,
    Guid PolicyId,
    int VersionNumber,
    string RuleName,
    bool WasCatchAll,
    decimal BasePrice,
    decimal Multiplier,
    decimal UnroundedPrice,
    decimal FinalPrice,
    DateTime CreatedOnUtc)
{
    internal static QuoteResponse FromEntity(Quote quote) =>
        new(quote.Id,
            quote.PolicyId,
            quote.VersionNumber,
            quote.RuleName,
            quote.WasCatchAll,
            quote.BasePrice,
            quote.Multiplier,
            quote.UnroundedPrice,
            quote.FinalPrice,
            quote.CreatedOnUtc);
}
