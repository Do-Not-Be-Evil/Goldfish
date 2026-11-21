using Goldfish.Domain.Abstractions;

namespace Goldfish.Domain.Policies;

public sealed class Policy : Entity
{
    /// <summary>Largest value a decimal(18,2) column can hold.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const int ModifiedByMaxLength = 64;

    private Policy(Guid id, PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy, DateTime createdOnUtc)
        : base(id)
    {
        Type = type;
        CoverageLimit = coverageLimit;
        BasePrice = basePrice;
        ModifiedBy = modifiedBy;
        CreatedOnUtc = createdOnUtc;
    }

    private Policy() { } // EF Core

    public PolicyType Type { get; private set; }
    public decimal CoverageLimit { get; private set; }
    public decimal BasePrice { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public string ModifiedBy { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Policy> Create(PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy, DateTime utcNow)
    {
        var validation = Validate(type, coverageLimit, basePrice, modifiedBy);
        if (validation.IsFailure)
        {
            return Result.Failure<Policy>(validation.Error);
        }

        return Result.Success(new Policy(Guid.CreateVersion7(), type, coverageLimit, basePrice, modifiedBy, utcNow));
    }

    public Result Update(PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy)
    {
        var validation = Validate(type, coverageLimit, basePrice, modifiedBy);
        if (validation.IsFailure)
        {
            return validation;
        }

        Type = type;
        CoverageLimit = coverageLimit;
        BasePrice = basePrice;
        ModifiedBy = modifiedBy;
        return Result.Success();
    }

    private static Result Validate(PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy)
    {
        if (!Enum.IsDefined(type))
        {
            return Result.Failure(PolicyErrors.InvalidType);
        }

        if (!IsValidAmount(coverageLimit))
        {
            return Result.Failure(PolicyErrors.InvalidCoverageLimit);
        }

        if (!IsValidAmount(basePrice))
        {
            return Result.Failure(PolicyErrors.InvalidBasePrice);
        }

        if (string.IsNullOrWhiteSpace(modifiedBy) || modifiedBy.Length > ModifiedByMaxLength)
        {
            throw new ArgumentException("A caller name of 1-64 characters is required.", nameof(modifiedBy));
        }

        return Result.Success();
    }

    private static bool IsValidAmount(decimal value) =>
        value > 0 && value <= MaxAmount && decimal.Round(value, 2) == value;
}
