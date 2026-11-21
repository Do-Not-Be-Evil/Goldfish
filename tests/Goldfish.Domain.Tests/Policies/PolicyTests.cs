using Goldfish.Domain.Policies;

namespace Goldfish.Domain.Tests.Policies;

public sealed class PolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_with_valid_values_succeeds()
    {
        var result = Policy.Create(PolicyType.Commercial, 2_000_000m, 1_234.57m, "portal", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyType.Commercial, result.Value.Type);
        Assert.Equal(2_000_000m, result.Value.CoverageLimit);
        Assert.Equal(1_234.57m, result.Value.BasePrice);
        Assert.Equal("portal", result.Value.ModifiedBy);
        Assert.Equal(Now, result.Value.CreatedOnUtc);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Update_with_valid_values_changes_fields_and_modified_by()
    {
        var policy = Policy.Create(PolicyType.Commercial, 2_000_000m, 1_234.57m, "portal", Now).Value;

        var result = policy.Update(PolicyType.Personal, 500_000m, 1_300m, "operator");

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyType.Personal, policy.Type);
        Assert.Equal(500_000m, policy.CoverageLimit);
        Assert.Equal(1_300m, policy.BasePrice);
        Assert.Equal("operator", policy.ModifiedBy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.255)]
    public void Create_rejects_invalid_coverage_limit(double coverageLimit)
    {
        var result = Policy.Create(PolicyType.Commercial, (decimal)coverageLimit, 100m, "portal", Now);

        Assert.Equal(PolicyErrors.InvalidCoverageLimit, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(100.255)]
    public void Create_rejects_invalid_base_price(double basePrice)
    {
        var result = Policy.Create(PolicyType.Commercial, 100m, (decimal)basePrice, "portal", Now);

        Assert.Equal(PolicyErrors.InvalidBasePrice, result.Error);
    }

    [Fact]
    public void Create_rejects_amount_above_column_range()
    {
        var result = Policy.Create(PolicyType.Commercial, Policy.MaxAmount + 0.01m, 100m, "portal", Now);

        Assert.Equal(PolicyErrors.InvalidCoverageLimit, result.Error);
    }

    [Fact]
    public void Create_accepts_trailing_zero_scale()
    {
        var result = Policy.Create(PolicyType.Commercial, 100.250m, 100.250m, "portal", Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_undefined_enum_value()
    {
        var result = Policy.Create((PolicyType)99, 100m, 100m, "portal", Now);

        Assert.Equal(PolicyErrors.InvalidType, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-caller-name-that-is-longer-than-sixty-four-characters-is-not-allowed")]
    public void Create_requires_a_caller_name_of_1_to_64_characters(string modifiedBy)
    {
        Assert.Throws<ArgumentException>(() => Policy.Create(PolicyType.Commercial, 100m, 100m, modifiedBy, Now));
    }

    [Fact]
    public void Update_rejects_invalid_type_and_coverage_limit()
    {
        var policy = Policy.Create(PolicyType.Commercial, 2_000_000m, 1_234.57m, "portal", Now).Value;

        Assert.Equal(PolicyErrors.InvalidType, policy.Update((PolicyType)99, 1m, 1m, "operator").Error);
        Assert.Equal(PolicyErrors.InvalidCoverageLimit, policy.Update(PolicyType.Commercial, 0m, 1m, "operator").Error);
    }

    [Fact]
    public void Update_rejects_invalid_value_and_leaves_policy_unchanged()
    {
        var policy = Policy.Create(PolicyType.Commercial, 2_000_000m, 1_234.57m, "portal", Now).Value;

        var result = policy.Update(PolicyType.Commercial, 2_000_000m, 0m, "operator");

        Assert.Equal(PolicyErrors.InvalidBasePrice, result.Error);
        Assert.Equal(1_234.57m, policy.BasePrice);
        Assert.Equal("portal", policy.ModifiedBy);
    }
}
