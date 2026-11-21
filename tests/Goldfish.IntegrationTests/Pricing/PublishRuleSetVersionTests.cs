using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Pricing;

/// <summary>One test per row of the procedure's error table, plus versioning, races, and dry run.</summary>
public sealed class PublishRuleSetVersionTests(LocalDbFixture database) : DatabaseTest(database)
{
    private const string Versions = "pricing.PricingRuleSetVersions";

    private Task<PublishResult> Publish(int baseVersion, params RuleInput[] rules) =>
        RuleSetPublisher.PublishAsync(Database.ConnectionString, baseVersion, rules);

    private async Task AssertRejected(int expectedNumber, Func<Task> publish, string? constraint = null)
    {
        var before = await Database.CountAsync(Versions);

        var ex = await Assert.ThrowsAsync<SqlException>(publish);

        Assert.Equal(expectedNumber, ex.Number);
        if (constraint is not null)
        {
            Assert.Contains(constraint, ex.Message);
        }
        Assert.Equal(before, await Database.CountAsync(Versions));
    }

    [Fact]
    public async Task Valid_publish_sets_next_version_number()
    {
        var first = await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);
        var second = await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString, baseVersionNumber: 1);

        Assert.Equal(1, first.NewVersionNumber);
        Assert.Equal(2, second.NewVersionNumber);
        Assert.Equal(4, await Database.CountAsync("pricing.PricingRules"));
    }

    [Theory]
    [InlineData("", "reviewer@example")]
    [InlineData("CHG-1", "  ")]
    public Task Blank_ticket_or_approver_is_50001(string ticket, string approver) =>
        AssertRejected(50001, () => RuleSetPublisher.PublishAsync(
            Database.ConnectionString, 0, [RuleInput.CatchAll()], changeTicketRef: ticket, approvedBy: approver));

    [Fact]
    public Task Empty_rule_set_is_50002() => AssertRejected(50002, () => Publish(0));

    [Fact]
    public async Task Stale_base_is_50003()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);

        await AssertRejected(50003, () => Publish(0, RuleInput.LargeCommercial(), RuleInput.CatchAll()));
    }

    [Fact]
    public Task No_catch_all_is_50004() => AssertRejected(50004, () => Publish(0, RuleInput.LargeCommercial()));

    [Fact]
    public Task Two_catch_alls_are_50004() =>
        AssertRejected(50004, () => Publish(0, RuleInput.CatchAll(999), new RuleInput(1000, "Other default", null, null, null, 1m)));

    [Fact]
    public Task Catch_all_without_largest_priority_number_is_50005() =>
        AssertRejected(50005, () => Publish(0, RuleInput.CatchAll(5), RuleInput.LargeCommercial()));

    [Fact]
    public async Task Concurrent_publishes_from_same_base_produce_one_version()
    {
        var attempts = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString)))
            .ToArray();

        var outcomes = await Task.WhenAll(attempts.Select(async t =>
        {
            try { await t; return (Exception?)null; }
            catch (Exception ex) { return ex; }
        }));

        Assert.Equal(1, outcomes.Count(e => e is null));
        Assert.All(outcomes.Where(e => e is not null), e =>
        {
            var sql = Assert.IsType<SqlException>(e);
            Assert.True(sql.Number is 2601 or 50003, $"Unexpected error {sql.Number}: {sql.Message}");
        });
        Assert.Equal(1, await Database.CountAsync(Versions));
    }

    [Fact]
    public Task Priority_below_one_violates_check() =>
        AssertRejected(547, () => Publish(0, new RuleInput(0, "Zero", "Commercial", null, null, 1m), RuleInput.CatchAll()),
            "CK_PricingRules_Priority_Min");

    [Fact]
    public Task Multiplier_not_positive_violates_check() =>
        AssertRejected(547, () => Publish(0, RuleInput.LargeCommercial(0m), RuleInput.CatchAll()),
            "CK_PricingRules_Multiplier_Positive");

    [Theory]
    [InlineData(500, 500)]
    [InlineData(600, 500)]
    public Task Minimum_not_below_maximum_violates_check(int min, int max) =>
        AssertRejected(547, () => Publish(0, new RuleInput(10, "Band", "Commercial", min, max, 1.2m), RuleInput.CatchAll()),
            "CK_PricingRules_CoverageRange");

    [Theory]
    [InlineData("Marine")]
    [InlineData("commercial")]
    public Task Unknown_policy_type_violates_check(string policyType) =>
        AssertRejected(547, () => Publish(0, new RuleInput(10, "Bad type", policyType, null, null, 1.2m), RuleInput.CatchAll()),
            "CK_PricingRules_PolicyType");

    [Fact]
    public Task Duplicate_priority_violates_unique_index() =>
        AssertRejected(2601, () => Publish(0,
                RuleInput.LargeCommercial(),
                new RuleInput(10, "Personal", "Personal", null, null, 0.9m),
                RuleInput.CatchAll()),
            "UX_PricingRules_Version_Priority");

    [Fact]
    public Task Duplicate_name_violates_unique_index() =>
        AssertRejected(2601, () => Publish(0,
                RuleInput.LargeCommercial(),
                new RuleInput(20, "Large commercial", "Personal", null, null, 0.9m),
                RuleInput.CatchAll()),
            "UX_PricingRules_Version_Name");

    [Fact]
    public async Task Duplicate_ticket_reference_violates_unique_index()
    {
        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 0, [RuleInput.CatchAll()], changeTicketRef: "CHG-42");

        await AssertRejected(2601,
            () => RuleSetPublisher.PublishAsync(Database.ConnectionString, 1, [RuleInput.CatchAll()], changeTicketRef: "CHG-42"),
            "UX_PricingRuleSetVersions_ChangeTicketRef");
    }

    [Fact]
    public async Task Dry_run_returns_diff_and_inserts_nothing()
    {
        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 0,
            [RuleInput.LargeCommercial(1.5m), new RuleInput(20, "Personal", "Personal", null, null, 0.9m), RuleInput.CatchAll()]);

        var result = await RuleSetPublisher.PublishAsync(Database.ConnectionString, 1,
            [RuleInput.LargeCommercial(1.6m), new RuleInput(30, "Small commercial", "Commercial", null, 1_000_000m, 1.1m), RuleInput.CatchAll()],
            dryRun: true);

        Assert.Equal(2, result.NewVersionNumber);
        Assert.Equal(1, await Database.CountAsync(Versions));
        Assert.Equal(3, await Database.CountAsync("pricing.PricingRules"));

        var rows = result.DryRunRows.ToDictionary(r => r.Name);
        Assert.Equal(4, rows.Count);
        Assert.Equal(("Changed", 1.5m, 1.6m, 1.0667m), (rows["Large commercial"].Change, rows["Large commercial"].OldMultiplier, rows["Large commercial"].NewMultiplier, rows["Large commercial"].Ratio));
        Assert.Equal("Removed", rows["Personal"].Change);
        Assert.Null(rows["Personal"].NewMultiplier);
        Assert.Equal("Added", rows["Small commercial"].Change);
        Assert.Null(rows["Small commercial"].OldMultiplier);
        Assert.Equal("Unchanged", rows["Default"].Change);
        Assert.Equal(1.0000m, rows["Default"].Ratio);
    }
}
