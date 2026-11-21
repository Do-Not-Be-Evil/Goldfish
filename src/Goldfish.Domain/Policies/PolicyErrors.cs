using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Policies;

public static class PolicyErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Policy.NotFound", "The policy with the specified identifier was not found.");

    public static readonly Error InvalidType =
        Error.Validation("Policy.InvalidType", "Policy type must be exactly 'Commercial' or 'Personal'.");

    public static readonly Error InvalidCoverageLimit =
        Error.Validation("Policy.InvalidCoverageLimit", "Coverage limit must be above 0, at most 2 decimal places, and within range.");

    public static readonly Error InvalidBasePrice =
        Error.Validation("Policy.InvalidBasePrice", "Base price must be above 0, at most 2 decimal places, and within range.");

    public static readonly Error Concurrency =
        Error.PreconditionFailed("Policy.Concurrency", "The policy was changed by another request. Reload it and retry with the new ETag.");
}
