using System.Data;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Infrastructure;

public sealed record RuleInput(
    int Priority,
    string Name,
    string? PolicyType,
    decimal? MinCoverageLimitExclusive,
    decimal? MaxCoverageLimitInclusive,
    decimal Multiplier)
{
    public static RuleInput CatchAll(int priority = 1000, decimal multiplier = 1.0m) =>
        new(priority, "Default", null, null, null, multiplier);

    public static RuleInput LargeCommercial(decimal multiplier = 1.5m) =>
        new(10, "Large commercial", "Commercial", 1_000_000m, null, multiplier);
}

public sealed record DryRunRow(string Name, decimal? OldMultiplier, decimal? NewMultiplier, decimal? Ratio, string Change);

public sealed record PublishResult(int NewVersionNumber, IReadOnlyList<DryRunRow> DryRunRows);

/// <summary>Calls pricing.PublishRuleSetVersion the same way a ticket script does.</summary>
public static class RuleSetPublisher
{
    private static int _ticketSequence;

    public static async Task<PublishResult> PublishAsync(
        string connectionString,
        int baseVersionNumber,
        IEnumerable<RuleInput> rules,
        bool dryRun = false,
        string? changeTicketRef = null,
        string approvedBy = "reviewer@example")
    {
        var table = new DataTable();
        table.Columns.Add("Priority", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("PolicyType", typeof(string));
        table.Columns.Add("MinCoverageLimitExclusive", typeof(decimal));
        table.Columns.Add("MaxCoverageLimitInclusive", typeof(decimal));
        table.Columns.Add("Multiplier", typeof(decimal));
        foreach (var rule in rules)
        {
            table.Rows.Add(
                rule.Priority,
                rule.Name,
                (object?)rule.PolicyType ?? DBNull.Value,
                (object?)rule.MinCoverageLimitExclusive ?? DBNull.Value,
                (object?)rule.MaxCoverageLimitInclusive ?? DBNull.Value,
                rule.Multiplier);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("pricing.PublishRuleSetVersion", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ChangeTicketRef",
            changeTicketRef ?? $"CHG-{Interlocked.Increment(ref _ticketSequence):D6}-{Guid.NewGuid():N}"[..40]);
        command.Parameters.AddWithValue("@ApprovedBy", approvedBy);
        command.Parameters.AddWithValue("@BaseVersionNumber", baseVersionNumber);
        command.Parameters.AddWithValue("@DryRun", dryRun);
        var rulesParameter = command.Parameters.AddWithValue("@Rules", table);
        rulesParameter.SqlDbType = SqlDbType.Structured;
        rulesParameter.TypeName = "pricing.PricingRuleInput";
        var output = command.Parameters.Add("@NewVersionNumber", SqlDbType.Int);
        output.Direction = ParameterDirection.Output;

        var rows = new List<DryRunRow>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add(new DryRunRow(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                    reader.GetString(4)));
            }
        }

        return new PublishResult((int)output.Value, rows);
    }

    public static Task<PublishResult> PublishDefaultAsync(string connectionString, int baseVersionNumber = 0, decimal largeCommercialMultiplier = 1.5m) =>
        PublishAsync(connectionString, baseVersionNumber,
            [RuleInput.LargeCommercial(largeCommercialMultiplier), RuleInput.CatchAll()]);
}
