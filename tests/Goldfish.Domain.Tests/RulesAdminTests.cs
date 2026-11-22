using Goldfish.Domain.RulesAdmin;

namespace Goldfish.Domain.Tests;

public sealed class RuleSetDiffTests
{
    private static readonly DraftRule CommercialLarge = new("Commercial large", 10, "Commercial", 1_000_000m, null, 1.5m);
    private static readonly DraftRule Default = new("Default", 1000, null, null, null, 1.0m);

    [Fact]
    public void Unchanged_rules_need_no_reason()
    {
        var diff = RuleSetDiff.Compute([CommercialLarge, Default], [CommercialLarge, Default], RatioBand.Default);

        Assert.All(diff, c => Assert.Equal(RuleChangeKind.Unchanged, c.Kind));
        Assert.All(diff, c => Assert.False(c.NeedsReason));
        Assert.All(diff, c => Assert.Equal(1m, c.Ratio));
    }

    [Theory]
    [InlineData(1.2, false)]   // ratio 0.8: on the band edge
    [InlineData(1.875, false)] // ratio 1.25: on the band edge
    [InlineData(15, true)]     // the 15-instead-of-1.5 typo
    [InlineData(1.19, true)]   // ratio 0.7933
    public void Multiplier_outside_the_band_needs_a_reason(double multiplier, bool needsReason)
    {
        var edited = CommercialLarge with { Multiplier = (decimal)multiplier };

        var change = RuleSetDiff.Compute([CommercialLarge, Default], [edited, Default], RatioBand.Default)[0];

        Assert.Equal(RuleChangeKind.Changed, change.Kind);
        Assert.Equal([nameof(DraftRule.Multiplier)], change.ChangedFields);
        Assert.Equal(needsReason, change.NeedsReason);
        Assert.Equal(needsReason, change.MissingReason);
    }

    [Fact]
    public void A_reason_satisfies_the_band_check()
    {
        var edited = CommercialLarge with { Multiplier = 3m, Reason = "Reinsurance cost doubled" };

        var change = RuleSetDiff.Compute([CommercialLarge, Default], [edited, Default], RatioBand.Default)[0];

        Assert.True(change.NeedsReason);
        Assert.False(change.MissingReason);
    }

    [Fact]
    public void Condition_changes_are_listed_field_by_field()
    {
        var edited = CommercialLarge with { Priority = 20, MaxCoverageLimitInclusive = 5_000_000m };

        var change = RuleSetDiff.Compute([CommercialLarge, Default], [edited, Default], RatioBand.Default)
            .Single(c => c.Name == CommercialLarge.Name);

        Assert.Equal([nameof(DraftRule.Priority), nameof(DraftRule.MaxCoverageLimitInclusive)], change.ChangedFields);
        Assert.False(change.NeedsReason);
    }

    [Fact]
    public void New_rules_always_need_a_reason_and_removed_rules_are_listed()
    {
        var added = new DraftRule("Personal small", 5, "Personal", null, 100_000m, 0.9m);

        var diff = RuleSetDiff.Compute([CommercialLarge, Default], [added, Default], RatioBand.Default);

        var addedChange = diff.Single(c => c.Kind == RuleChangeKind.Added);
        Assert.True(addedChange.MissingReason);
        Assert.Null(addedChange.Ratio);
        var removed = diff.Single(c => c.Kind == RuleChangeKind.Removed);
        Assert.Equal(CommercialLarge.Name, removed.Name);
        Assert.False(removed.NeedsReason);
    }

    [Fact]
    public void Names_match_without_regard_to_case_like_the_database()
    {
        var renamed = Default with { Name = "DEFAULT" };

        var change = Assert.Single(RuleSetDiff.Compute([Default], [renamed], RatioBand.Default));

        Assert.Equal(RuleChangeKind.Changed, change.Kind);
        Assert.Equal([nameof(DraftRule.Name)], change.ChangedFields);
    }
}

public sealed class RuleSetValidatorTests
{
    private static readonly DraftRule Default = new("Default", 1000, null, null, null, 1.0m);

    [Fact]
    public void A_valid_set_has_no_problems()
    {
        Assert.Empty(RuleSetValidator.Validate([new("Commercial large", 10, "Commercial", 1_000_000m, null, 1.5m), Default]));
    }

    [Fact]
    public void Empty_set_is_a_problem()
    {
        Assert.Single(RuleSetValidator.Validate([]));
    }

    public static TheoryData<DraftRule[], string> Invalid => new()
    {
        { [Default with { Name = "" }], nameof(DraftRule.Name) },
        { [Default with { Name = new string('x', 201) }], nameof(DraftRule.Name) },
        { [Default with { Name = " Default" }], nameof(DraftRule.Name) },
        { [Default with { Priority = 0 }], nameof(DraftRule.Priority) },
        { [Default with { Multiplier = 0 }], nameof(DraftRule.Multiplier) },
        { [Default with { Multiplier = 1.23456m }], nameof(DraftRule.Multiplier) },
        { [Default with { Multiplier = 100_000m }], nameof(DraftRule.Multiplier) },
        { [new("A", 1, "commercial", null, null, 1m), Default], nameof(DraftRule.PolicyType) },
        { [new("A", 1, null, 10.001m, null, 1m), Default], nameof(DraftRule.MinCoverageLimitExclusive) },
        { [new("A", 1, null, 100m, 100m, 1m), Default], nameof(DraftRule.MaxCoverageLimitInclusive) },
        { [new("A", 1, "Personal", null, null, 1m), new("a", 2, "Commercial", null, null, 1m), Default], nameof(DraftRule.Name) },
        { [new("A", 5, "Personal", null, null, 1m), new("B", 5, "Commercial", null, null, 1m), Default], nameof(DraftRule.Priority) },
        { [new("A", 1, "Personal", null, null, 1m)], "Rules" },
        { [Default, Default with { Name = "Other default", Priority = 2000 }], "Rules" },
        { [new("A", 2000, "Personal", null, null, 1m), Default], nameof(DraftRule.Priority) },
        { [Default with { Reason = new string('x', 1001) }], nameof(DraftRule.Reason) },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_sets_report_the_field(DraftRule[] rules, string field)
    {
        Assert.Contains(RuleSetValidator.Validate(rules), p => p.Field == field);
    }
}

public sealed class ChangeRequestStateTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();

    [Fact]
    public void New_request_is_a_draft() =>
        Assert.Equal(ChangeRequestStatus.Draft, ChangeRequestState.Derive(R1, [], false));

    [Fact]
    public void Submitting_the_latest_revision_makes_it_submitted() =>
        Assert.Equal(ChangeRequestStatus.Submitted,
            ChangeRequestState.Derive(R1, [new(ChangeRequestEventType.Submitted, R1)], false));

    [Fact]
    public void Comments_do_not_change_the_status() =>
        Assert.Equal(ChangeRequestStatus.Submitted,
            ChangeRequestState.Derive(R1, [new(ChangeRequestEventType.Submitted, R1), new(ChangeRequestEventType.Comment, R1)], false));

    [Fact]
    public void Rejected_until_a_new_revision_is_saved()
    {
        StatusEvent[] events = [new(ChangeRequestEventType.Submitted, R1), new(ChangeRequestEventType.Rejected, R1)];

        Assert.Equal(ChangeRequestStatus.Rejected, ChangeRequestState.Derive(R1, events, false));
        Assert.Equal(ChangeRequestStatus.Draft, ChangeRequestState.Derive(R2, events, false));
    }

    [Fact]
    public void Withdrawn_and_published_are_final()
    {
        Assert.Equal(ChangeRequestStatus.Withdrawn,
            ChangeRequestState.Derive(R2, [new(ChangeRequestEventType.Withdrawn, R1)], false));
        Assert.Equal(ChangeRequestStatus.Published,
            ChangeRequestState.Derive(R1, [new(ChangeRequestEventType.Submitted, R1)], true));
        Assert.False(ChangeRequestState.CanEdit(ChangeRequestStatus.Submitted));
        Assert.False(ChangeRequestState.CanEdit(ChangeRequestStatus.Published));
    }
}
