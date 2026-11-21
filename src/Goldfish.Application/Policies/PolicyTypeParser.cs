using Goldfish.Domain.Abstractions;
using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies;

/// <summary>
/// Accepts only the exact, case-sensitive type names. <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/>
/// is not used because it accepts numeric strings and can ignore case.
/// </summary>
internal static class PolicyTypeParser
{
    public static Result<PolicyType> Parse(string? value) => value switch
    {
        nameof(PolicyType.Commercial) => Result.Success(PolicyType.Commercial),
        nameof(PolicyType.Personal) => Result.Success(PolicyType.Personal),
        _ => Result.Failure<PolicyType>(PolicyErrors.InvalidType)
    };
}
