using System.Net;
using Goldfish.IntegrationTests.Infrastructure;
using Goldfish.Web.Security;

namespace Goldfish.IntegrationTests.Web;

/// <summary>The Razor Pages change-request workflow, publishing through rulesadmin.ApproveAndPublish.</summary>
public sealed class ChangePagesTests(LocalDbFixture database) : WebTest(database)
{
    private static IEnumerable<KeyValuePair<string, string>> Rule(int i, string name, int priority, string type, string min, string multiplier, string reason) =>
    [
        F($"Rules[{i}].Name", name),
        F($"Rules[{i}].Priority", priority.ToString()),
        F($"Rules[{i}].PolicyType", type),
        F($"Rules[{i}].MinCoverageLimitExclusive", min),
        F($"Rules[{i}].MaxCoverageLimitInclusive", ""),
        F($"Rules[{i}].Multiplier", multiplier),
        F($"Rules[{i}].Reason", reason),
    ];

    private async Task<string> DraftAndSubmitAsync(HttpClient drafter)
    {
        var created = await drafter.PostFormAsync("/Changes?handler=Create", "", [F("Title", "Initial rules")], Ct, "/Changes");
        var edit = Path(created);
        Assert.StartsWith("/Changes/Edit/", edit);

        var saved = await drafter.PostFormAsync(edit, "",
        [
            F("ExpectedRevision", "1"),
            F("Action", "save"),
            .. Rule(0, "Large commercial", 10, "Commercial", "1000000", "1.5", "Initial rule set"),
            .. Rule(1, "Default", 1000, "", "", "1.0", "Initial rule set"),
        ], Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
        var details = Path(saved);
        Assert.StartsWith("/Changes/Details/", details);

        var submitted = await drafter.PostFormAsync($"{details}?handler=Submit", "", [F("Comment", "Please review")], Ct, details);
        Assert.Contains("Submitted", await submitted.Content.ReadAsStringAsync(Ct));
        return details;
    }

    [Fact]
    public async Task Analyst_drafts_and_manager_approves()
    {
        var details = await DraftAndSubmitAsync(Analyst());
        var manager = Manager();
        var page = await manager.GetStringAsync(details, Ct);
        Assert.Contains("Approve and publish", page);
        Assert.Contains("data-approve-form", page);

        var approved = await manager.PostFormAsync($"{details}?handler=Approve", "",
            [F("RevisionId", HiddenValue(page, "RevisionId")), F("Confirm", "true")], Ct, details);

        Assert.Contains("Published as version 1", await approved.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, await Database.CountAsync("pricing.PricingRuleSetVersions"));
        Assert.Contains("Version 1", await manager.GetStringAsync("/", Ct)); // start page reads it from the API
    }

    [Fact]
    public async Task Approval_without_confirmation_publishes_nothing()
    {
        var details = await DraftAndSubmitAsync(Analyst());
        var manager = Manager();
        var page = await manager.GetStringAsync(details, Ct);

        var response = await manager.PostFormAsync($"{details}?handler=Approve", "", [F("RevisionId", HiddenValue(page, "RevisionId"))], Ct, details);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Database.CountAsync("pricing.PricingRuleSetVersions"));
    }

    [Fact]
    public async Task The_drafter_cannot_approve_even_with_the_manager_role()
    {
        var dual = Web.CreateClient("dual-oid", "Dee Both", WebRoles.Analyst, WebRoles.Manager);
        var details = await DraftAndSubmitAsync(dual);

        var page = await dual.GetStringAsync(details, Ct);

        Assert.DoesNotContain("Approve and publish", page);
        Assert.Contains("cannot approve or reject it", page);
    }

    [Fact]
    public async Task Edit_page_renders_bindable_names_and_the_script_data()
    {
        var analyst = Analyst();
        var edit = Path(await analyst.PostFormAsync("/Changes?handler=Create", "", [F("Title", "Names")], Ct, "/Changes"));

        var added = await analyst.PostFormAsync(edit, "",
            [F("ExpectedRevision", "1"), F("Action", "add"), .. Rule(0, "Default", 1000, "", "", "1.0", "")], Ct);
        var html = await added.Content.ReadAsStringAsync(Ct);

        Assert.Contains("name=\"Rules[0].Name\"", html);
        Assert.Contains("name=\"Rules[1].Multiplier\"", html);
        Assert.Contains("name=\"Rules[__i__].Name\"", html); // template cloned by rule-editor.ts
        Assert.Contains("id=\"rule-editor-data\"", html);
        Assert.Contains("\"band\":{\"min\":0.8", html);
        Assert.Equal(1, await Database.CountAsync("rulesadmin.Revisions"));
    }

    [Fact]
    public async Task Invalid_rules_are_not_saved()
    {
        var analyst = Analyst();
        var edit = Path(await analyst.PostFormAsync("/Changes?handler=Create", "", [F("Title", "Bad")], Ct, "/Changes"));

        var response = await analyst.PostFormAsync(edit, "",
            [F("ExpectedRevision", "1"), F("Action", "save"), .. Rule(0, "Only", 10, "Commercial", "", "1.23456", "")], Ct);

        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 4 decimal places", html);
        Assert.Contains("exactly one catch-all", html);
    }

    [Fact]
    public async Task Policy_editors_without_a_pricing_role_cannot_open_pricing_changes()
    {
        var editor = Editor();

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/Changes", Ct)).StatusCode);
        Assert.DoesNotContain("Pricing changes", await editor.GetStringAsync("/Policies", Ct));
    }
}
