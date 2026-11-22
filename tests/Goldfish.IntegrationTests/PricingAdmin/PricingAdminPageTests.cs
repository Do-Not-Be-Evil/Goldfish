using System.Net;
using System.Text.RegularExpressions;
using Goldfish.IntegrationTests.Infrastructure;
using Goldfish.PricingAdmin.Security;

namespace Goldfish.IntegrationTests.PricingAdmin;

/// <summary>Drives the admin app's pages over HTTP, posting the same forms a browser would.</summary>
public sealed partial class PricingAdminPageTests(LocalDbFixture database) : DatabaseTest(database)
{
    private PricingAdminFactory _factory = default!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _factory = new PricingAdminFactory(Database.ConnectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await base.DisposeAsync();
    }

    private HttpClient Analyst() => _factory.CreateClient("analyst-oid", "Ana Analyst", PricingRoles.Analyst);
    private HttpClient Manager() => _factory.CreateClient("manager-oid", "Mia Manager", PricingRoles.Manager);
    private HttpClient DualRole() => _factory.CreateClient("dual-oid", "Dee Both", PricingRoles.Analyst, PricingRoles.Manager);

    private static KeyValuePair<string, string> F(string name, string value) => new(name, value);

    private static IEnumerable<KeyValuePair<string, string>> Rule(int i, string name, int priority, string policyType,
        string min, string max, string multiplier, string reason) =>
    [
        F($"Model.Rules[{i}].Name", name),
        F($"Model.Rules[{i}].Priority", priority.ToString()),
        F($"Model.Rules[{i}].PolicyType", policyType),
        F($"Model.Rules[{i}].MinCoverageLimitExclusive", min),
        F($"Model.Rules[{i}].MaxCoverageLimitInclusive", max),
        F($"Model.Rules[{i}].Multiplier", multiplier),
        F($"Model.Rules[{i}].Reason", reason),
    ];

    /// <summary>Starts a request, saves an initial rule set and submits it. Returns the request's URL.</summary>
    private async Task<string> DraftAndSubmitAsync(HttpClient drafter)
    {
        var created = await drafter.PostFormAsync("/", "create", [F("Create.Title", "Initial rules")], Ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var editUrl = created.RequestMessage!.RequestUri!.AbsolutePath;
        Assert.EndsWith("/edit", editUrl);

        var saved = await drafter.PostFormAsync(editUrl, "edit",
        [
            F("Model.ExpectedRevision", "1"),
            F("Model.Action", "save"),
            .. Rule(0, "Large commercial", 10, "Commercial", "1000000", "", "1.5", "Initial rule set"),
            .. Rule(1, "Default", 1000, "", "", "", "1.0", "Initial rule set"),
        ], Ct);
        var savedHtml = await saved.Content.ReadAsStringAsync(Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedHtml);
        var detailsUrl = saved.RequestMessage!.RequestUri!.AbsolutePath;
        Assert.DoesNotContain("/edit", detailsUrl);

        var submitted = await drafter.PostFormAsync(detailsUrl, "submit", [F("Submit.Comment", "Please review")], Ct);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Contains("Submitted", await submitted.Content.ReadAsStringAsync(Ct));

        return detailsUrl;
    }

    private static string RevisionIdFrom(string html) => RevisionPattern().Match(html).Groups[1].Value;

    [GeneratedRegex("name=\"Approve.RevisionId\" value=\"([0-9a-f-]{36})\"")]
    private static partial Regex RevisionPattern();

    [Fact]
    public async Task Anonymous_users_are_challenged()
    {
        var response = await _factory.CreateClient().GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_without_a_pricing_role_are_forbidden()
    {
        var response = await _factory.CreateClient("someone-oid", "Someone").GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_drafts_and_manager_approves_through_the_pages()
    {
        var url = await DraftAndSubmitAsync(Analyst());

        var manager = Manager();
        var page = await manager.GetStringAsync(url, Ct);
        Assert.Contains("Approve and publish", page);

        var approved = await manager.PostFormAsync(url, "approve",
            [F("Approve.RevisionId", RevisionIdFrom(page)), F("Approve.Confirm", "true"), F("Approve.Comment", "OK")], Ct);

        var html = await approved.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Contains("Published as version 1", html);
        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task Approving_without_confirming_publishes_nothing()
    {
        var url = await DraftAndSubmitAsync(Analyst());
        var manager = Manager();
        var page = await manager.GetStringAsync(url, Ct);

        var response = await manager.PostFormAsync(url, "approve", [F("Approve.RevisionId", RevisionIdFrom(page))], Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Confirm that approving publishes", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(0, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task The_drafter_sees_no_approve_button_even_with_the_manager_role()
    {
        var dual = DualRole();
        var url = await DraftAndSubmitAsync(dual);

        var page = await dual.GetStringAsync(url, Ct);

        Assert.DoesNotContain("Approve and publish", page);
        Assert.Contains("cannot approve or reject it", page);
    }

    [Fact]
    public async Task Invalid_rules_are_not_saved_and_problems_are_shown()
    {
        var analyst = Analyst();
        var created = await analyst.PostFormAsync("/", "create", [F("Create.Title", "Bad rules")], Ct);
        var editUrl = created.RequestMessage!.RequestUri!.AbsolutePath;

        var response = await analyst.PostFormAsync(editUrl, "edit",
        [
            F("Model.ExpectedRevision", "1"),
            F("Model.Action", "save"),
            .. Rule(0, "Only rule", 10, "Commercial", "", "", "1.23456", ""),
        ], Ct);

        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 4 decimal places", html);
        Assert.Contains("exactly one catch-all", html);
        Assert.Equal(1, await Database.CountAsync("rulesadmin.Revisions"));
    }

    [Fact]
    public async Task Add_rule_keeps_the_typed_rows_without_saving()
    {
        var analyst = Analyst();
        var created = await analyst.PostFormAsync("/", "create", [F("Create.Title", "Growing")], Ct);
        var editUrl = created.RequestMessage!.RequestUri!.AbsolutePath;

        var response = await analyst.PostFormAsync(editUrl, "edit",
        [
            F("Model.ExpectedRevision", "1"),
            F("Model.Action", "add"),
            .. Rule(0, "Default", 1000, "", "", "", "1.0", ""),
        ], Ct);

        // The rendered input names must be the ones the server binds, or a browser's save would lose the rows.
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Matches("name=\"Model.Rules\\[0\\].Name\"[^>]*value=\"Default\"|value=\"Default\"[^>]*name=\"Model.Rules\\[0\\].Name\"", html);
        Assert.Contains("name=\"Model.Rules[1].Multiplier\"", html);
        Assert.DoesNotContain("name=\"row.", html);
        Assert.Equal(1, await Database.CountAsync("rulesadmin.Revisions"));
    }
}
