using Goldfish.Domain.RulesAdmin;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.RulesAdmin;

/// <summary>
/// Keeps RuleSetValidator in step with pricing.PublishRuleSetVersion and the pricing.PricingRules constraints:
/// every set the database rejects must also be rejected by the form, and a set the form accepts must publish.
/// </summary>
public sealed class RuleSetValidatorParityTests(LocalDbFixture database) : DatabaseTest(database)
{
    private static readonly DraftRule Default = new("Default", 1000, null, null, null, 1.0m);

    public static TheoryData<string, DraftRule[]> RejectedByDatabase => new()
    {
        { "50004 no catch-all", [new("A", 1, "Personal", null, null, 1m)] },
        { "50004 two catch-alls", [Default, Default with { Name = "Other", Priority = 2000 }] },
        { "50005 catch-all not last", [new("A", 2000, "Personal", null, null, 1m), Default] },
        { "2601 duplicate priority", [new("A", 5, "Personal", null, null, 1m), new("B", 5, "Commercial", null, null, 1m), Default] },
        { "2601 duplicate name, other case", [new("A", 1, "Personal", null, null, 1m), new("a", 2, "Commercial", null, null, 1m), Default] },
        { "547 priority below 1", [new("A", 0, "Personal", null, null, 1m), Default] },
        { "547 multiplier zero", [new("A", 1, "Personal", null, null, 0m), Default] },
        { "547 lower-case policy type", [new("A", 1, "personal", null, null, 1m), Default] },
        { "547 empty coverage range", [new("A", 1, null, 500m, 500m, 1m), Default] },
    };

    [Theory]
    [MemberData(nameof(RejectedByDatabase))]
    public async Task Sets_the_database_rejects_are_rejected_by_the_form(string label, DraftRule[] rules)
    {
        Assert.NotEmpty(RuleSetValidator.Validate(rules));

        var ex = await Assert.ThrowsAsync<SqlException>(() => DryRunAsync(rules));
        Assert.True(ex.Number is 50004 or 50005 or 2601 or 547, $"{label}: unexpected SQL error {ex.Number}");
    }

    [Fact]
    public async Task A_set_the_form_accepts_publishes()
    {
        DraftRule[] rules =
        [
            new("Large commercial", 10, "Commercial", 1_000_000m, null, 1.5m),
            new("Small personal", 20, "Personal", null, 250_000.50m, 0.9375m),
            Default,
        ];

        Assert.Empty(RuleSetValidator.Validate(rules));
        var result = await DryRunAsync(rules);
        Assert.Equal(3, result.DryRunRows.Count);
    }

    private Task<PublishResult> DryRunAsync(IEnumerable<DraftRule> rules) =>
        RuleSetPublisher.PublishAsync(
            Database.ConnectionString,
            0,
            rules.Select(r => new RuleInput(r.Priority, r.Name, r.PolicyType, r.MinCoverageLimitExclusive, r.MaxCoverageLimitInclusive, r.Multiplier)),
            dryRun: true);
}
