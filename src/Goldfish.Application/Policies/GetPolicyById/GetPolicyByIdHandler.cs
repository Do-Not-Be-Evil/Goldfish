using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies.GetPolicyById;

public sealed record GetPolicyByIdQuery(Guid Id);

public sealed class GetPolicyByIdHandler(IPolicyRepository policyRepository)
{
    public async Task<Result<PolicyResponse>> HandleAsync(
        GetPolicyByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetByIdAsync(query.Id, cancellationToken);

        return policy is null
            ? Result.Failure<PolicyResponse>(PolicyErrors.NotFound)
            : Result.Success(PolicyResponse.FromEntity(policy));
    }
}
