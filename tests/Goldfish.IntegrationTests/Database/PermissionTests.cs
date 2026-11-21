using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Database;

/// <summary>Runs the real grant script against users without logins, then checks what each user can do.</summary>
public sealed class PermissionTests(LocalDbFixture database) : DatabaseTest(database)
{
    private const string ApiUser = "api_test";
    private const string PublisherUser = "publisher_test";

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        await ExecuteAsync($"""
            IF USER_ID('{ApiUser}') IS NULL CREATE USER [{ApiUser}] WITHOUT LOGIN;
            IF USER_ID('{PublisherUser}') IS NULL CREATE USER [{PublisherUser}] WITHOUT LOGIN;
            """);

        var script = SqlScriptLoader.RepositoryPath("db", "scripts", "grant-permissions.sql");
        var variables = new Dictionary<string, string> { ["ApiUser"] = ApiUser, ["PublisherUser"] = PublisherUser };

        // Run twice: the second run must succeed unchanged.
        await SqlScriptLoader.RunAsync(Database.ConnectionString, script, variables, Ct);
        await SqlScriptLoader.RunAsync(Database.ConnectionString, script, variables, Ct);
    }

    private async Task<object?> ExecuteAsync(string sql, string? asUser = null)
    {
        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = asUser is null ? sql : $"EXECUTE AS USER = '{asUser}'; {sql}; REVERT;";
        return await command.ExecuteScalarAsync(Ct);
    }

    private async Task<bool> HasPermissionAsync(string user, string securable, string securableClass, string permission) =>
        (int)(await ExecuteAsync(
            $"SELECT HAS_PERMS_BY_NAME('{securable}', '{securableClass}', '{permission}')", user))! == 1;

    [Theory]
    [InlineData(ApiUser, "UPDATE pricing.PricingRuleSetVersions SET ApprovedBy = ApprovedBy WHERE 1 = 0")]
    [InlineData(ApiUser, "DELETE pricing.PricingRules WHERE 1 = 0")]
    [InlineData(ApiUser, "UPDATE pricing.Quotes SET FinalPrice = FinalPrice WHERE 1 = 0")]
    [InlineData(ApiUser, "DELETE pricing.Quotes WHERE 1 = 0")]
    [InlineData(PublisherUser, "UPDATE pricing.PricingRules SET Multiplier = Multiplier WHERE 1 = 0")]
    [InlineData(PublisherUser, "DELETE pricing.PricingRuleSetVersions WHERE 1 = 0")]
    [InlineData(PublisherUser, "DELETE pricing.Quotes WHERE 1 = 0")]
    [InlineData(PublisherUser, "INSERT pricing.PricingRules (VersionId, Priority, Name, Multiplier) VALUES (NEWID(), 1, 'x', 1)")]
    public async Task Edits_to_pricing_tables_are_denied(string user, string statement)
    {
        var ex = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(statement, user));

        Assert.Equal(229, ex.Number); // permission denied
    }

    [Fact]
    public async Task Publisher_can_publish_through_the_procedure()
    {
        await ExecuteAsync("""
            DECLARE @Rules pricing.PricingRuleInput;
            INSERT @Rules (Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
            VALUES (1000, N'Default', NULL, NULL, NULL, 1.0);
            DECLARE @New int;
            EXEC pricing.PublishRuleSetVersion
                @ChangeTicketRef = N'CHG-PERM', @ApprovedBy = N'dba', @BaseVersionNumber = 0,
                @Rules = @Rules, @NewVersionNumber = @New OUTPUT
            """, PublisherUser);

        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Theory]
    [InlineData("pricing.Quotes", "INSERT")]
    [InlineData("pricing.Quotes", "SELECT")]
    [InlineData("pricing.PricingRules", "SELECT")]
    [InlineData("pricing.PricingRuleSetVersions", "SELECT")]
    [InlineData("dbo.Policies", "UPDATE")]
    [InlineData("dbo.PoliciesHistory", "SELECT")]
    [InlineData("dbo.Products", "SELECT")]
    [InlineData("dbo.Products", "INSERT")]
    public async Task Api_user_has_required_rights(string table, string permission)
    {
        Assert.True(await HasPermissionAsync(ApiUser, table, "OBJECT", permission));
    }

    [Fact]
    public async Task Verify_script_runs_and_reports_no_problems()
    {
        var text = (await File.ReadAllTextAsync(SqlScriptLoader.RepositoryPath("db", "scripts", "verify-permissions.sql"), Ct))
            .Replace("$(ApiUser)", ApiUser, StringComparison.Ordinal)
            .Replace("$(PublisherUser)", PublisherUser, StringComparison.Ordinal);

        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(text, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);

        var rows = 0;
        while (await reader.ReadAsync(Ct))
        {
            rows++;
            Assert.True(reader.IsDBNull(reader.GetOrdinal("Problem")),
                $"{reader["UserName"]} {reader["Permission"]} {reader["ObjectName"]}: {reader["Problem"]}");
        }
        Assert.True(rows > 0);
    }

    [Fact]
    public async Task Api_user_cannot_execute_the_publish_procedure()
    {
        Assert.False(await HasPermissionAsync(ApiUser, "pricing.PublishRuleSetVersion", "OBJECT", "EXECUTE"));
    }
}
