using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Database;

/// <summary>Runs the real grant script against users without logins, then checks what each user can do.</summary>
public sealed class PermissionTests(LocalDbFixture database) : DatabaseTest(database)
{
    private const string ApiUser = "api_test";
    private const string RulesAdminUser = "rules_admin_test";
    private const string PublisherUser = "publisher_test";

    private static readonly Dictionary<string, string> Variables = new()
    {
        ["ApiUser"] = ApiUser,
        ["RulesAdminUser"] = RulesAdminUser,
        ["PublisherUser"] = PublisherUser,
    };

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        // The publisher starts with its old grants, so the test proves the script takes them away.
        await ExecuteAsync($"""
            IF USER_ID('{ApiUser}') IS NULL CREATE USER [{ApiUser}] WITHOUT LOGIN;
            IF USER_ID('{RulesAdminUser}') IS NULL CREATE USER [{RulesAdminUser}] WITHOUT LOGIN;
            IF USER_ID('{PublisherUser}') IS NULL CREATE USER [{PublisherUser}] WITHOUT LOGIN;
            REVOKE EXECUTE ON OBJECT::pricing.PublishRuleSetVersion FROM [{PublisherUser}];
            GRANT EXECUTE ON OBJECT::pricing.PublishRuleSetVersion TO [{PublisherUser}];
            GRANT EXECUTE, REFERENCES ON TYPE::pricing.PricingRuleInput TO [{PublisherUser}];
            """);

        var script = SqlScriptLoader.RepositoryPath("db", "scripts", "grant-permissions.sql");

        // Run twice: the second run must succeed unchanged.
        await SqlScriptLoader.RunAsync(Database.ConnectionString, script, Variables, Ct);
        await SqlScriptLoader.RunAsync(Database.ConnectionString, script, Variables, Ct);
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

    private const string PublishDirectly = """
        DECLARE @Rules pricing.PricingRuleInput;
        INSERT @Rules (Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
        VALUES (1000, N'Default', NULL, NULL, NULL, 1.0);
        DECLARE @New int;
        EXEC pricing.PublishRuleSetVersion
            @ChangeTicketRef = N'CHG-PERM', @ApprovedBy = N'dba', @BaseVersionNumber = 0,
            @Rules = @Rules, @NewVersionNumber = @New OUTPUT
        """;

    [Theory]
    [InlineData(ApiUser, "UPDATE pricing.PricingRuleSetVersions SET ApprovedBy = ApprovedBy WHERE 1 = 0")]
    [InlineData(ApiUser, "DELETE pricing.PricingRules WHERE 1 = 0")]
    [InlineData(ApiUser, "UPDATE pricing.Quotes SET FinalPrice = FinalPrice WHERE 1 = 0")]
    [InlineData(ApiUser, "DELETE pricing.Quotes WHERE 1 = 0")]
    [InlineData(ApiUser, "SELECT COUNT(*) FROM rulesadmin.ChangeRequests")]
    [InlineData(RulesAdminUser, "UPDATE pricing.PricingRules SET Multiplier = Multiplier WHERE 1 = 0")]
    [InlineData(RulesAdminUser, "INSERT pricing.PricingRules (VersionId, Priority, Name, Multiplier) VALUES (NEWID(), 1, 'x', 1)")]
    [InlineData(RulesAdminUser, "UPDATE rulesadmin.RevisionRules SET Multiplier = Multiplier WHERE 1 = 0")]
    [InlineData(RulesAdminUser, "DELETE rulesadmin.Revisions WHERE 1 = 0")]
    [InlineData(RulesAdminUser, "DELETE rulesadmin.ChangeRequestEvents WHERE 1 = 0")]
    [InlineData(RulesAdminUser, "UPDATE rulesadmin.ChangeRequests SET Title = Title WHERE 1 = 0")]
    [InlineData(RulesAdminUser, "INSERT rulesadmin.Approvals (ChangeRequestId, RevisionId, ApproverOid, ApproverName, PublishedVersionNumber) VALUES (NEWID(), NEWID(), 'x', 'x', 1)")]
    [InlineData(RulesAdminUser, "UPDATE rulesadmin.RatioBand SET MaxRatio = 100")]
    [InlineData(RulesAdminUser, "SELECT COUNT(*) FROM pricing.Quotes")]
    [InlineData(PublisherUser, "DELETE pricing.PricingRuleSetVersions WHERE 1 = 0")]
    [InlineData(PublisherUser, "INSERT pricing.PricingRules (VersionId, Priority, Name, Multiplier) VALUES (NEWID(), 1, 'x', 1)")]
    [InlineData(PublisherUser, PublishDirectly)]
    [InlineData(RulesAdminUser, PublishDirectly)]
    [InlineData(ApiUser, PublishDirectly)]
    public async Task Disallowed_statements_are_refused(string user, string statement)
    {
        var ex = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(statement, user));

        Assert.Equal(229, ex.Number); // permission denied
    }

    [Theory]
    [InlineData(ApiUser, "pricing.Quotes", "INSERT")]
    [InlineData(ApiUser, "pricing.Quotes", "SELECT")]
    [InlineData(ApiUser, "pricing.PricingRules", "SELECT")]
    [InlineData(ApiUser, "pricing.PricingRuleSetVersions", "SELECT")]
    [InlineData(ApiUser, "dbo.Policies", "UPDATE")]
    [InlineData(ApiUser, "dbo.PoliciesHistory", "SELECT")]
    [InlineData(RulesAdminUser, "pricing.PricingRules", "SELECT")]
    [InlineData(RulesAdminUser, "pricing.PricingRuleSetVersions", "SELECT")]
    [InlineData(RulesAdminUser, "rulesadmin.ChangeRequests", "INSERT")]
    [InlineData(RulesAdminUser, "rulesadmin.Revisions", "INSERT")]
    [InlineData(RulesAdminUser, "rulesadmin.RevisionRules", "INSERT")]
    [InlineData(RulesAdminUser, "rulesadmin.ChangeRequestEvents", "INSERT")]
    [InlineData(RulesAdminUser, "rulesadmin.Approvals", "SELECT")]
    [InlineData(RulesAdminUser, "rulesadmin.RatioBand", "SELECT")]
    [InlineData(RulesAdminUser, "rulesadmin.ApproveAndPublish", "EXECUTE")]
    public async Task Users_have_required_rights(string user, string securable, string permission)
    {
        Assert.True(await HasPermissionAsync(user, securable, "OBJECT", permission));
    }

    [Fact]
    public async Task Rules_admin_user_can_publish_through_the_wrapper()
    {
        var change = Guid.NewGuid();
        await ExecuteAsync($"""
            INSERT rulesadmin.ChangeRequests (Id, Reference, Title, CreatedByOid, CreatedByName)
            VALUES ('{change}', 'GFA-PERM', 'Permission test', 'analyst-oid', 'Analyst');
            INSERT rulesadmin.Revisions (ChangeRequestId, RevisionNumber, BaseVersionNumber, AuthorOid, AuthorName)
            VALUES ('{change}', 1, 0, 'analyst-oid', 'Analyst');
            INSERT rulesadmin.RevisionRules (RevisionId, Priority, Name, Multiplier, Reason)
            SELECT Id, 1000, N'Default', 1.0, N'Initial rule set' FROM rulesadmin.Revisions WHERE ChangeRequestId = '{change}';
            INSERT rulesadmin.ChangeRequestEvents (ChangeRequestId, RevisionId, Type, ActorOid, ActorName)
            SELECT '{change}', Id, 'Submitted', 'analyst-oid', 'Analyst' FROM rulesadmin.Revisions WHERE ChangeRequestId = '{change}';
            DECLARE @Revision uniqueidentifier = (SELECT Id FROM rulesadmin.Revisions WHERE ChangeRequestId = '{change}');
            DECLARE @New int;
            EXEC rulesadmin.ApproveAndPublish @ChangeRequestId = '{change}', @RevisionId = @Revision,
                @ApproverOid = N'manager-oid', @ApproverName = N'Manager', @NewVersionNumber = @New OUTPUT
            """, RulesAdminUser);

        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
        Assert.Equal(1, await Database.CountAsync("rulesadmin.Approvals"));
    }

    [Fact]
    public async Task Verify_script_runs_and_reports_no_problems()
    {
        var text = await File.ReadAllTextAsync(SqlScriptLoader.RepositoryPath("db", "scripts", "verify-permissions.sql"), Ct);
        foreach (var (name, value) in Variables)
        {
            text = text.Replace($"$({name})", value, StringComparison.Ordinal);
        }

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

    [Theory]
    [InlineData(ApiUser)]
    [InlineData(RulesAdminUser)]
    [InlineData(PublisherUser)]
    public async Task Only_the_wrapper_can_publish(string user)
    {
        Assert.False(await HasPermissionAsync(user, "pricing.PublishRuleSetVersion", "OBJECT", "EXECUTE"));
    }
}
