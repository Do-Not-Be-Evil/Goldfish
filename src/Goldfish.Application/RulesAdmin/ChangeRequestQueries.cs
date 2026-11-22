using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;

namespace Goldfish.Application.RulesAdmin;

public sealed class ListChangeRequestsHandler(IRulesAdminStore store)
{
    public Task<IReadOnlyList<ChangeRequestSummary>> HandleAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);
}

public sealed class GetChangeRequestHandler(IRulesAdminStore store)
{
    public async Task<Result<ChangeRequestDetails>> HandleAsync(
        Guid id,
        Actor actor,
        CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(id, cancellationToken);
        if (data is null)
        {
            return Result.Failure<ChangeRequestDetails>(RulesAdminErrors.NotFound);
        }

        return Result.Success(await ChangeRequestEvaluator.EvaluateAsync(store, data, actor, cancellationToken));
    }
}

/// <summary>Builds the diff, problems, staleness and allowed actions for the latest revision.</summary>
internal static class ChangeRequestEvaluator
{
    public static async Task<ChangeRequestDetails> EvaluateAsync(
        IRulesAdminStore store,
        ChangeRequestData data,
        Actor actor,
        CancellationToken cancellationToken)
    {
        var revision = data.LatestRevision;
        var latestVersion = await store.GetLatestVersionNumberAsync(cancellationToken);
        var band = await store.GetRatioBandAsync(cancellationToken);
        var baseRules = await store.GetVersionRulesAsync(revision.BaseVersionNumber, cancellationToken);

        var diff = RuleSetDiff.Compute(baseRules, revision.Rules, band);
        var problems = RuleSetValidator.Validate(revision.Rules);
        var status = data.Status;
        var isStale = status != ChangeRequestStatus.Published && revision.BaseVersionNumber != latestVersion;

        return new ChangeRequestDetails(
            data,
            status,
            latestVersion,
            isStale,
            band,
            diff,
            problems,
            ChangeRequestPolicy.Evaluate(data, actor, isStale, problems, diff));
    }
}
