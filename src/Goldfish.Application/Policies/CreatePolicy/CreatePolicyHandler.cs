using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies.CreatePolicy;

public sealed record CreatePolicyCommand(string PolicyType, decimal CoverageLimit, decimal BasePrice, string ModifiedBy);

public sealed class CreatePolicyHandler(
    IPolicyRepository policyRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<PolicyResponse>> HandleAsync(
        CreatePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        var type = PolicyTypeParser.Parse(command.PolicyType);
        if (type.IsFailure)
        {
            return Result.Failure<PolicyResponse>(type.Error);
        }

        var policy = Policy.Create(type.Value, command.CoverageLimit, command.BasePrice, command.ModifiedBy,
            timeProvider.GetUtcNow().UtcDateTime);
        if (policy.IsFailure)
        {
            return Result.Failure<PolicyResponse>(policy.Error);
        }

        policyRepository.Add(policy.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(PolicyResponse.FromEntity(policy.Value));
    }
}
