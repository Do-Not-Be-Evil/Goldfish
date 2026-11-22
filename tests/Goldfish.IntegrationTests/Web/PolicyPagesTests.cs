using System.Net;
using Goldfish.IntegrationTests.Infrastructure;

namespace Goldfish.IntegrationTests.Web;

/// <summary>The MVC policy pages, going through the real Goldfish.Api with its API key.</summary>
public sealed class PolicyPagesTests(LocalDbFixture database) : WebTest(database)
{
    private async Task<string> CreatePolicyAsync(HttpClient editor, string type = "Commercial", string coverage = "2000000", string price = "1000")
    {
        var response = await editor.PostFormAsync("/Policies/Create", "",
            [F("PolicyType", type), F("CoverageLimit", coverage), F("BasePrice", price)], Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Policy created.", html);
        return Path(response);
    }

    [Fact]
    public async Task Anonymous_users_are_challenged_and_users_without_a_role_are_forbidden()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Web.CreateClient().GetAsync("/Policies", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Web.CreateClient("x", "Nobody").GetAsync("/Policies", Ct)).StatusCode);
    }

    [Fact]
    public async Task Editor_creates_a_policy_and_everyone_can_list_it()
    {
        var details = await CreatePolicyAsync(Editor());

        Assert.StartsWith("/Policies/Details/", details);
        var list = await Analyst().GetStringAsync("/Policies", Ct);
        Assert.Contains("2,000,000.00", list);
        Assert.Contains("1,000.00", list);
        Assert.Equal(1, await Database.CountAsync("dbo.Policies"));
    }

    [Fact]
    public async Task Only_policy_editors_can_create_or_edit()
    {
        var analyst = Analyst();

        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/Policies", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/Policies/Create", Ct)).StatusCode);
        Assert.DoesNotContain("New policy", await analyst.GetStringAsync("/Policies", Ct));
    }

    [Fact]
    public async Task Invalid_values_are_reported_and_nothing_is_saved()
    {
        var response = await Editor().PostFormAsync("/Policies/Create", "",
            [F("PolicyType", "Commercial"), F("CoverageLimit", "2000000"), F("BasePrice", "10.123")], Ct);

        Assert.Contains("Use at most 2 decimal places.", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(0, await Database.CountAsync("dbo.Policies"));
    }

    [Fact]
    public async Task Editor_changes_the_base_price()
    {
        var editor = Editor();
        var details = await CreatePolicyAsync(editor);
        var edit = details.Replace("/Details/", "/Edit/");
        var page = await editor.GetStringAsync(edit, Ct);

        var saved = await editor.PostFormAsync(edit, "",
            [F("PolicyType", "Commercial"), F("CoverageLimit", "2000000"), F("BasePrice", "1250"), F("ETag", HiddenValue(page, "ETag"))], Ct);

        var html = await saved.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Policy saved.", html);
        Assert.Contains("1,250.00", html);
        Assert.Contains("data-original=\"1000", page); // policy-form.ts compares against the saved values
    }

    [Fact]
    public async Task A_concurrent_edit_is_shown_instead_of_overwritten()
    {
        var first = Editor("editor-1");
        var second = Editor("editor-2");
        var edit = (await CreatePolicyAsync(first)).Replace("/Details/", "/Edit/");
        var staleEtag = HiddenValue(await first.GetStringAsync(edit, Ct), "ETag");

        var other = await second.PostFormAsync(edit, "",
            [F("PolicyType", "Commercial"), F("CoverageLimit", "2000000"), F("BasePrice", "1100"),
             F("ETag", HiddenValue(await second.GetStringAsync(edit, Ct), "ETag"))], Ct);
        Assert.Contains("Policy saved.", await other.Content.ReadAsStringAsync(Ct));

        var conflict = await first.PostFormAsync(edit, "",
            [F("PolicyType", "Commercial"), F("CoverageLimit", "2000000"), F("BasePrice", "900"), F("ETag", staleEtag)], Ct);
        var conflictHtml = await conflict.Content.ReadAsStringAsync(Ct);

        Assert.Contains("was changed by", conflictHtml);
        Assert.Contains("1,100.00", conflictHtml);
        var freshEtag = HiddenValue(conflictHtml, "ETag");
        Assert.NotEqual(staleEtag, freshEtag);

        var retry = await first.PostFormAsync(edit, "",
            [F("PolicyType", "Commercial"), F("CoverageLimit", "2000000"), F("BasePrice", "900"), F("ETag", freshEtag)], Ct);
        Assert.Contains("900.00", await retry.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Compiled_typescript_is_served()
    {
        var client = Analyst();

        foreach (var script in new[] { "/js/rule-editor.js", "/js/approve-form.js", "/js/policy-form.js" })
        {
            var response = await client.GetAsync(script, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
