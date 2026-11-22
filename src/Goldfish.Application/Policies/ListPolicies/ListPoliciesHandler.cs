using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies.ListPolicies;

public sealed record ListPoliciesQuery(int Skip = 0, int Take = ListPoliciesQuery.DefaultTake, string? PolicyType = null)
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;
}

public sealed record PolicyPage(IReadOnlyList<PolicyResponse> Items, int Total, int Skip, int Take);

public sealed class ListPoliciesHandler(IPolicyRepository policyRepository)
{
    public async Task<Result<PolicyPage>> HandleAsync(ListPoliciesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Skip < 0 || query.Take < 1 || query.Take > ListPoliciesQuery.MaxTake)
        {
            return Result.Failure<PolicyPage>(PolicyErrors.InvalidPage);
        }

        PolicyType? type = null;
        if (!string.IsNullOrEmpty(query.PolicyType))
        {
            var parsed = PolicyTypeParser.Parse(query.PolicyType);
            if (parsed.IsFailure)
            {
                return Result.Failure<PolicyPage>(parsed.Error);
            }

            type = parsed.Value;
        }

        var (items, total) = await policyRepository.ListAsync(query.Skip, query.Take, type, cancellationToken);
        return Result.Success(new PolicyPage(items.Select(PolicyResponse.FromEntity).ToList(), total, query.Skip, query.Take));
    }
}
