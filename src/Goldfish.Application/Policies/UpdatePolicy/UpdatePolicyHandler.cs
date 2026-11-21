using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies.UpdatePolicy;

public sealed record UpdatePolicyCommand(
    Guid Id,
    string PolicyType,
    decimal CoverageLimit,
    decimal BasePrice,
    byte[] ExpectedRowVersion,
    string ModifiedBy);

public sealed class UpdatePolicyHandler(IPolicyRepository policyRepository, IUnitOfWork unitOfWork)
{
    public async Task<Result<PolicyResponse>> HandleAsync(
        UpdatePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetForUpdateAsync(command.Id, command.ExpectedRowVersion, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<PolicyResponse>(PolicyErrors.NotFound);
        }

        var type = PolicyTypeParser.Parse(command.PolicyType);
        if (type.IsFailure)
        {
            return Result.Failure<PolicyResponse>(type.Error);
        }

        var update = policy.Update(type.Value, command.CoverageLimit, command.BasePrice, command.ModifiedBy);
        if (update.IsFailure)
        {
            return Result.Failure<PolicyResponse>(update.Error);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Failure<PolicyResponse>(PolicyErrors.Concurrency);
        }

        return Result.Success(PolicyResponse.FromEntity(policy));
    }
}
