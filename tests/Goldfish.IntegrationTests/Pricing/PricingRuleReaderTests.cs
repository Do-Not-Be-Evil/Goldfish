using Goldfish.Domain.Policies;
using Goldfish.Domain.Pricing;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Goldfish.IntegrationTests.Pricing;

public sealed class PricingRuleReaderTests(LocalDbFixture database) : DatabaseTest(database)
{
    private async Task<Domain.Abstractions.Result<PricingRuleMatch>> FindAsync(PolicyType type, decimal coverageLimit)
    {
        await using var services = Database.CreateServices();
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPricingRuleReader>();
        return await reader.FindMatchAsync(type, coverageLimit, Ct);
    }

    [Fact]
    public async Task No_version_is_no_active_version()
    {
        var result = await FindAsync(PolicyType.Commercial, 2_000_000m);

        Assert.Equal(PricingErrors.NoActiveVersion, result.Error);
    }

    [Fact]
    public async Task Exact_boundary_falls_through_to_catch_all()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);

        var result = await FindAsync(PolicyType.Commercial, 1_000_000m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Default", result.Value.RuleName);
        Assert.True(result.Value.WasCatchAll);
        Assert.Equal(1.0m, result.Value.Multiplier);
    }

    [Fact]
    public async Task Just_above_boundary_matches_large_commercial()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);

        var result = await FindAsync(PolicyType.Commercial, 1_000_000.01m);

        Assert.Equal("Large commercial", result.Value.RuleName);
        Assert.False(result.Value.WasCatchAll);
        Assert.Equal(1.5m, result.Value.Multiplier);
        Assert.Equal(1, result.Value.VersionNumber);
    }

    [Fact]
    public async Task Lower_priority_number_wins_when_two_rules_match()
    {
        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 0,
        [
            new RuleInput(20, "All commercial", "Commercial", null, null, 1.2m),
            new RuleInput(10, "Large commercial", "Commercial", 1_000_000m, null, 1.5m),
            RuleInput.CatchAll()
        ]);

        var result = await FindAsync(PolicyType.Commercial, 2_000_000m);

        Assert.Equal("Large commercial", result.Value.RuleName);
        Assert.Equal(10, result.Value.Priority);
    }

    [Fact]
    public async Task Maximum_is_inclusive()
    {
        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 0,
        [
            new RuleInput(10, "Small commercial", "Commercial", null, 500_000m, 1.1m),
            RuleInput.CatchAll()
        ]);

        Assert.Equal("Small commercial", (await FindAsync(PolicyType.Commercial, 500_000m)).Value.RuleName);
        Assert.Equal("Default", (await FindAsync(PolicyType.Commercial, 500_000.01m)).Value.RuleName);
    }

    [Fact]
    public async Task Personal_policy_never_matches_commercial_rule()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);

        var result = await FindAsync(PolicyType.Personal, 5_000_000m);

        Assert.Equal("Default", result.Value.RuleName);
    }

    [Fact]
    public async Task Newly_published_version_is_used_on_next_call()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString, largeCommercialMultiplier: 1.5m);
        Assert.Equal(1.5m, (await FindAsync(PolicyType.Commercial, 2_000_000m)).Value.Multiplier);

        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString, baseVersionNumber: 1, largeCommercialMultiplier: 1.6m);
        var result = await FindAsync(PolicyType.Commercial, 2_000_000m);

        Assert.Equal(1.6m, result.Value.Multiplier);
        Assert.Equal(2, result.Value.VersionNumber);
    }

    [Fact]
    public async Task Rows_written_outside_procedure_without_match_are_invalid_active_version()
    {
        await using (var connection = new SqlConnection(Database.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @Id uniqueidentifier = NEWID();
                INSERT pricing.PricingRuleSetVersions (Id, VersionNumber, ChangeTicketRef, ApprovedBy) VALUES (@Id, 1, 'CHG-BYPASS', 'dba');
                INSERT pricing.PricingRules (VersionId, Priority, Name, PolicyType, Multiplier) VALUES (@Id, 10, 'Personal only', 'Personal', 1.1);
                """;
            await command.ExecuteNonQueryAsync(Ct);
        }

        var result = await FindAsync(PolicyType.Commercial, 2_000_000m);

        Assert.Equal(PricingErrors.InvalidActiveVersion, result.Error);
    }

    [Fact]
    public async Task Lowercase_policy_type_is_rejected_by_binary_collation()
    {
        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT dbo.Policies (Id, PolicyType, CoverageLimit, BasePrice, CreatedOnUtc, ModifiedBy)
            VALUES (NEWID(), 'commercial', 1, 1, SYSUTCDATETIME(), 'test');
            """;

        var ex = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(Ct));

        Assert.Equal(547, ex.Number);
        Assert.Contains("CK_Policies_PolicyType", ex.Message);
    }
}
