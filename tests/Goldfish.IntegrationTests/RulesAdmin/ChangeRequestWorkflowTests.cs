using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.Abstractions;
using Goldfish.Domain.RulesAdmin;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Goldfish.IntegrationTests.RulesAdmin;

/// <summary>End-to-end change-request workflow through the real handlers, store and rulesadmin.ApproveAndPublish.</summary>
public sealed class ChangeRequestWorkflowTests(LocalDbFixture database) : DatabaseTest(database)
{
    private static readonly Actor Analyst = new("analyst-oid", "Ana Analyst", IsAnalyst: true, IsManager: false);
    private static readonly Actor OtherAnalyst = new("analyst2-oid", "Bo Analyst", IsAnalyst: true, IsManager: false);
    private static readonly Actor Manager = new("manager-oid", "Mia Manager", IsAnalyst: false, IsManager: true);
    private static readonly Actor OtherManager = new("manager2-oid", "Max Manager", IsAnalyst: false, IsManager: true);
    private static readonly Actor DualRole = new("dual-oid", "Dee Both", IsAnalyst: true, IsManager: true);

    private static readonly DraftRule Default = new("Default", 1000, null, null, null, 1.0m, "Initial rule set");
    private static readonly DraftRule LargeCommercial = new("Large commercial", 10, "Commercial", 1_000_000m, null, 1.5m, "Initial rule set");

    private ServiceProvider _services = default!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _services = Database.CreateRulesAdminServices();
    }

    public override async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await base.DisposeAsync();
    }

    private async Task<T> With<THandler, T>(Func<THandler, Task<T>> call) where THandler : notnull
    {
        await using var scope = _services.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    private Task<Result<Guid>> CreateAsync(Actor actor, string title = "Monthly update") =>
        With<CreateChangeRequestHandler, Result<Guid>>(h => h.HandleAsync(actor, title, Ct));

    private Task<Result<ChangeRequestDetails>> GetAsync(Guid id, Actor actor) =>
        With<GetChangeRequestHandler, Result<ChangeRequestDetails>>(h => h.HandleAsync(id, actor, Ct));

    private Task<Result> SaveAsync(Guid id, Actor actor, int expectedRevision, params DraftRule[] rules) =>
        With<SaveRevisionHandler, Result>(h => h.HandleAsync(id, actor, expectedRevision, rules, Ct));

    private Task<Result> SubmitAsync(Guid id, Actor actor) =>
        With<SubmitChangeRequestHandler, Result>(h => h.HandleAsync(id, actor, null, Ct));

    private async Task<Result<int>> ApproveAsync(Guid id, Actor actor, Guid? revisionId = null)
    {
        var revision = revisionId ?? (await GetAsync(id, actor)).Value.Data.LatestRevision.Id;
        return await With<ApproveChangeRequestHandler, Result<int>>(h => h.HandleAsync(id, revision, actor, true, "Looks right", Ct));
    }

    /// <summary>Creates a request, saves the given rules as revision 2, and submits it.</summary>
    private async Task<Guid> SubmittedAsync(Actor drafter, params DraftRule[] rules)
    {
        var id = (await CreateAsync(drafter)).Value;
        Assert.True((await SaveAsync(id, drafter, 1, rules)).IsSuccess);
        var submit = await SubmitAsync(id, drafter);
        Assert.True(submit.IsSuccess, submit.IsFailure ? submit.Error.Description : null);
        return id;
    }

    private async Task<int> CallWrapperAsync(Guid id, Guid revisionId, string approverOid)
    {
        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand("rulesadmin.ApproveAndPublish", connection) { CommandType = System.Data.CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@ChangeRequestId", id);
        command.Parameters.AddWithValue("@RevisionId", revisionId);
        command.Parameters.AddWithValue("@ApproverOid", approverOid);
        command.Parameters.AddWithValue("@ApproverName", "Direct caller");
        var output = command.Parameters.Add("@NewVersionNumber", System.Data.SqlDbType.Int);
        output.Direction = System.Data.ParameterDirection.Output;
        await command.ExecuteNonQueryAsync(Ct);
        return (int)output.Value;
    }

    private async Task<Guid> LatestRevisionIdAsync(Guid id) => (await GetAsync(id, Manager)).Value.Data.LatestRevision.Id;

    [Fact]
    public async Task Analyst_drafts_manager_approves_and_the_version_goes_live()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);

        var approved = await ApproveAsync(id, Manager);

        Assert.True(approved.IsSuccess, approved.IsFailure ? approved.Error.Description : null);
        Assert.Equal(1, approved.Value);

        var details = (await GetAsync(id, Manager)).Value;
        Assert.Equal(ChangeRequestStatus.Published, details.Status);
        Assert.Equal(1, details.Data.Approval!.PublishedVersionNumber);
        Assert.Equal(details.Data.LatestRevision.Id, details.Data.Approval.RevisionId);

        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(
            "SELECT ChangeTicketRef + N' ' + ApprovedBy FROM pricing.PricingRuleSetVersions WHERE VersionNumber = 1", connection);
        Assert.Equal($"{details.Data.Reference} manager-oid|Mia Manager", (string?)await command.ExecuteScalarAsync(Ct));
        Assert.Equal(2, await Database.CountAsync("pricing.PricingRules"));
    }

    [Fact]
    public async Task New_request_starts_from_the_live_rules()
    {
        var first = await SubmittedAsync(Analyst, LargeCommercial, Default);
        await ApproveAsync(first, Manager);

        var id = (await CreateAsync(OtherAnalyst)).Value;
        var revision = (await GetAsync(id, OtherAnalyst)).Value.Data.LatestRevision;

        Assert.Equal(1, revision.BaseVersionNumber);
        Assert.Equal(["Large commercial", "Default"], revision.Rules.Select(r => r.Name));
        Assert.All(revision.Rules, r => Assert.Null(r.Reason));
    }

    [Fact]
    public async Task A_dual_role_drafter_cannot_approve_their_own_request()
    {
        var id = await SubmittedAsync(DualRole, LargeCommercial, Default);

        var details = (await GetAsync(id, DualRole)).Value;
        var approved = await ApproveAsync(id, DualRole);

        Assert.False(details.Actions.CanApprove);
        Assert.False(details.Actions.CanReject);
        Assert.True(approved.IsFailure);
        Assert.Equal(0, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task The_database_refuses_a_contributor_even_when_the_app_is_bypassed()
    {
        var id = await SubmittedAsync(DualRole, LargeCommercial, Default);

        var revisionId = await LatestRevisionIdAsync(id);
        var ex = await Assert.ThrowsAsync<SqlException>(() => CallWrapperAsync(id, revisionId, DualRole.Oid));

        Assert.Equal(51006, ex.Number);
        Assert.Equal(0, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task A_manager_who_edited_the_draft_cannot_approve_it()
    {
        var id = (await CreateAsync(Analyst)).Value;
        Assert.True((await SaveAsync(id, DualRole, 1, LargeCommercial, Default)).IsSuccess);
        Assert.True((await SubmitAsync(id, Analyst)).IsSuccess);

        var approved = await ApproveAsync(id, DualRole);

        Assert.True(approved.IsFailure);
        Assert.Equal(51006, (await Assert.ThrowsAsync<SqlException>(
            async () => await CallWrapperAsync(id, await LatestRevisionIdAsync(id), DualRole.Oid))).Number);
    }

    [Fact]
    public async Task A_stale_draft_cannot_be_approved_and_restart_rebases_it()
    {
        var first = await SubmittedAsync(Analyst, LargeCommercial, Default);
        var second = await SubmittedAsync(OtherAnalyst, LargeCommercial with { Multiplier = 1.6m }, Default);
        await ApproveAsync(first, Manager);

        var refused = await ApproveAsync(second, Manager);
        var direct = await Assert.ThrowsAsync<SqlException>(async () => await CallWrapperAsync(second, await LatestRevisionIdAsync(second), Manager.Oid));

        Assert.Equal(RulesAdminErrors.Stale, refused.Error);
        Assert.Equal(50003, direct.Number);

        // Withdraw is not needed: the analyst rebases, re-enters the change, and submits again.
        Assert.True((await With<RejectChangeRequestHandler, Result>(h => h.HandleAsync(second, Manager, "Stale, please rebase", Ct))).IsSuccess);
        var restart = await With<RestartFromLatestHandler, Result>(h => h.HandleAsync(second, OtherAnalyst, Ct));
        Assert.True(restart.IsSuccess, restart.IsFailure ? restart.Error.Description : null);

        var rebased = (await GetAsync(second, OtherAnalyst)).Value;
        Assert.Equal(1, rebased.Data.LatestRevision.BaseVersionNumber);
        Assert.False(rebased.IsStale);
        Assert.Equal(ChangeRequestStatus.Draft, rebased.Status);

        var revision = rebased.Data.LatestRevision.RevisionNumber;
        Assert.True((await SaveAsync(second, OtherAnalyst, revision, LargeCommercial with { Multiplier = 1.6m, Reason = null }, Default with { Reason = null })).IsSuccess);
        Assert.True((await SubmitAsync(second, OtherAnalyst)).IsSuccess);
        Assert.Equal(2, (await ApproveAsync(second, Manager)).Value);
    }

    [Fact]
    public async Task Out_of_band_change_without_reason_cannot_be_submitted_or_published()
    {
        var first = await SubmittedAsync(Analyst, LargeCommercial, Default);
        await ApproveAsync(first, Manager);

        var id = (await CreateAsync(Analyst)).Value;
        Assert.True((await SaveAsync(id, Analyst, 1, LargeCommercial with { Multiplier = 15m, Reason = null }, Default with { Reason = null })).IsSuccess);

        var submit = await SubmitAsync(id, Analyst);
        Assert.True(submit.IsFailure);
        Assert.Contains("reason", submit.Error.Description, StringComparison.OrdinalIgnoreCase);

        // Bypass the app: write the Submitted event directly, then call the wrapper.
        var revisionId = await LatestRevisionIdAsync(id);
        await using (var connection = new SqlConnection(Database.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new SqlCommand($"""
                INSERT rulesadmin.ChangeRequestEvents (ChangeRequestId, RevisionId, Type, ActorOid, ActorName)
                VALUES ('{id}', '{revisionId}', 'Submitted', 'analyst-oid', 'Ana Analyst')
                """, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        var ex = await Assert.ThrowsAsync<SqlException>(() => CallWrapperAsync(id, revisionId, Manager.Oid));
        Assert.Equal(51007, ex.Number);
        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task Out_of_band_change_with_reason_is_published()
    {
        var first = await SubmittedAsync(Analyst, LargeCommercial, Default);
        await ApproveAsync(first, Manager);

        var id = (await CreateAsync(Analyst)).Value;
        Assert.True((await SaveAsync(id, Analyst, 1,
            LargeCommercial with { Multiplier = 3m, Reason = "Reinsurance cost doubled" }, Default with { Reason = null })).IsSuccess);
        Assert.True((await SubmitAsync(id, Analyst)).IsSuccess);

        Assert.Equal(2, (await ApproveAsync(id, Manager)).Value);
    }

    [Fact]
    public async Task Approving_an_older_revision_than_the_latest_is_refused()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);
        var first = (await GetAsync(id, Manager)).Value.Data.Revisions[0].Id;

        Assert.Equal(RulesAdminErrors.Concurrency, (await ApproveAsync(id, Manager, first)).Error);
        Assert.Equal(51004, (await Assert.ThrowsAsync<SqlException>(() => CallWrapperAsync(id, first, Manager.Oid))).Number);
    }

    [Fact]
    public async Task Two_managers_approving_at_once_publish_exactly_once()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);
        var revisionId = await LatestRevisionIdAsync(id);

        var results = await Task.WhenAll(
            Task.Run(async () => { try { return await CallWrapperAsync(id, revisionId, Manager.Oid); } catch (SqlException e) { return -e.Number; } }, Ct),
            Task.Run(async () => { try { return await CallWrapperAsync(id, revisionId, OtherManager.Oid); } catch (SqlException e) { return -e.Number; } }, Ct));

        Assert.Single(results, r => r == 1);
        Assert.Single(results, r => r == -51003);
        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
        Assert.Equal(1, await Database.CountAsync("rulesadmin.Approvals"));
    }

    [Fact]
    public async Task Retrying_your_own_approval_returns_the_same_version()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);
        var revisionId = await LatestRevisionIdAsync(id);
        Assert.Equal(1, (await ApproveAsync(id, Manager)).Value);

        await using var scope = _services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRulesAdminStore>();
        var retry = await store.ApproveAndPublishAsync(id, revisionId, Manager, null, Ct);
        var other = await store.ApproveAndPublishAsync(id, revisionId, OtherManager, null, Ct);

        Assert.Equal(1, retry.Value);
        Assert.Equal(RulesAdminErrors.AlreadyPublished, other.Error);
        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task Two_saves_from_the_same_revision_conflict()
    {
        var id = (await CreateAsync(Analyst)).Value;

        var first = await SaveAsync(id, Analyst, 1, LargeCommercial, Default);
        var second = await SaveAsync(id, OtherAnalyst, 1, Default);

        Assert.True(first.IsSuccess);
        Assert.Equal(RulesAdminErrors.Concurrency, second.Error);
    }

    [Fact]
    public async Task Submitted_requests_cannot_be_edited_and_rejection_needs_a_reason()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);

        Assert.Equal(RulesAdminErrors.WrongStatus, (await SaveAsync(id, Analyst, 2, Default)).Error);
        Assert.Equal(RulesAdminErrors.CommentRequired,
            (await With<RejectChangeRequestHandler, Result>(h => h.HandleAsync(id, Manager, "  ", Ct))).Error);

        Assert.True((await With<RejectChangeRequestHandler, Result>(h => h.HandleAsync(id, Manager, "Wrong threshold", Ct))).IsSuccess);
        Assert.Equal(ChangeRequestStatus.Rejected, (await GetAsync(id, Analyst)).Value.Status);

        Assert.True((await SaveAsync(id, Analyst, 2, LargeCommercial with { MinCoverageLimitExclusive = 2_000_000m }, Default)).IsSuccess);
        Assert.Equal(ChangeRequestStatus.Draft, (await GetAsync(id, Analyst)).Value.Status);
    }

    [Fact]
    public async Task Analysts_cannot_approve_and_managers_cannot_draft()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);

        Assert.True((await ApproveAsync(id, OtherAnalyst)).IsFailure);
        Assert.True((await CreateAsync(Manager)).IsFailure);
        Assert.True((await SaveAsync(id, Manager, 2, Default)).IsFailure);
    }

    [Fact]
    public async Task Approval_needs_explicit_confirmation()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);
        var revisionId = await LatestRevisionIdAsync(id);

        var result = await With<ApproveChangeRequestHandler, Result<int>>(h => h.HandleAsync(id, revisionId, Manager, false, null, Ct));

        Assert.Equal(RulesAdminErrors.ConfirmRequired, result.Error);
    }

    [Fact]
    public async Task Withdrawn_requests_are_final()
    {
        var id = await SubmittedAsync(Analyst, LargeCommercial, Default);

        Assert.True((await With<WithdrawChangeRequestHandler, Result>(h => h.HandleAsync(id, Analyst, "Not needed", Ct))).IsSuccess);

        var details = (await GetAsync(id, Manager)).Value;
        Assert.Equal(ChangeRequestStatus.Withdrawn, details.Status);
        Assert.False(details.Actions.CanApprove);
        Assert.True((await ApproveAsync(id, Manager)).IsFailure);
    }
}
