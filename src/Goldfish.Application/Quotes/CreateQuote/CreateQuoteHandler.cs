using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;
using Goldfish.Domain.Quotes;
using Microsoft.Extensions.Logging;

namespace Goldfish.Application.Quotes.CreateQuote;

public sealed record CreateQuoteCommand(Guid PolicyId, string RequestedBy);

public sealed partial class CreateQuoteHandler(
    IPolicyRepository policyRepository,
    IPricingRuleReader pricingRuleReader,
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CreateQuoteHandler> logger)
{
    public async Task<Result<QuoteResponse>> HandleAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetByIdAsync(command.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Fail(PolicyErrors.NotFound, command);
        }

        var match = await pricingRuleReader.FindMatchAsync(policy.Type, policy.CoverageLimit, cancellationToken);
        if (match.IsFailure)
        {
            return Fail(match.Error, command);
        }

        // A single save: the context enables EnableRetryOnFailure, which rejects user-started transactions.
        var quote = Quote.Create(policy, match.Value, command.RequestedBy, timeProvider.GetUtcNow().UtcDateTime);
        quoteRepository.Add(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogQuoteCreated(quote.Id, quote.PolicyId, quote.VersionNumber, quote.RuleId, quote.WasCatchAll,
            quote.Multiplier, quote.FinalPrice, quote.RequestedBy);

        return Result.Success(QuoteResponse.FromEntity(quote));
    }

    private Result<QuoteResponse> Fail(Error error, CreateQuoteCommand command)
    {
        var level = error == PricingErrors.InvalidActiveVersion ? LogLevel.Error
            : error == PricingErrors.NoActiveVersion ? LogLevel.Warning
            : LogLevel.Information;
        LogQuoteFailed(level, error.Code, command.PolicyId, command.RequestedBy);
        return Result.Failure<QuoteResponse>(error);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Quote {QuoteId} for policy {PolicyId} priced by version {VersionNumber} rule {RuleId} (catch-all {WasCatchAll}) multiplier {Multiplier} final {FinalPrice} for {RequestedBy}")]
    private partial void LogQuoteCreated(Guid quoteId, Guid policyId, int versionNumber, Guid ruleId, bool wasCatchAll,
        decimal multiplier, decimal finalPrice, string requestedBy);

    [LoggerMessage(Message = "Quote request for policy {PolicyId} failed with {ErrorCode} for {RequestedBy}")]
    private partial void LogQuoteFailed(LogLevel level, string errorCode, Guid policyId, string requestedBy);
}
