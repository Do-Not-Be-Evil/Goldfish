// TEMPORARY: renders real PricingAdmin pages to HTML files for a visual check. Delete after use.
using System.Net;
using System.Text.RegularExpressions;
using Goldfish.IntegrationTests.Infrastructure;
using Goldfish.PricingAdmin.Security;

namespace Goldfish.IntegrationTests.PricingAdmin;

public sealed class ZzStyleSnapshot(LocalDbFixture database) : DatabaseTest(database)
{
    private static KeyValuePair<string, string> F(string n, string v) => new(n, v);

    [Fact]
    public async Task Snapshot()
    {
        var outDir = Environment.GetEnvironmentVariable("GOLDFISH_SNAPSHOT_DIR");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        await using var factory = new PricingAdminFactory(Database.ConnectionString);
        var analyst = factory.CreateClient("analyst-oid", "Ana Analyst", PricingRoles.Analyst);
        var manager = factory.CreateClient("manager-oid", "Mia Manager", PricingRoles.Manager);

        var created = await analyst.PostFormAsync("/", "create", [F("Create.Title", "Raise large commercial multiplier")], Ct);
        var editUrl = created.RequestMessage!.RequestUri!.AbsolutePath;
        var saved = await analyst.PostFormAsync(editUrl, "edit",
        [
            F("Model.ExpectedRevision", "1"), F("Model.Action", "save"),
            F("Model.Rules[0].Name", "Large commercial"), F("Model.Rules[0].Priority", "10"), F("Model.Rules[0].PolicyType", "Commercial"),
            F("Model.Rules[0].MinCoverageLimitExclusive", "1000000"), F("Model.Rules[0].MaxCoverageLimitInclusive", ""),
            F("Model.Rules[0].Multiplier", "1.5"), F("Model.Rules[0].Reason", "Loss ratio above target"),
            F("Model.Rules[1].Name", "Default"), F("Model.Rules[1].Priority", "1000"), F("Model.Rules[1].PolicyType", ""),
            F("Model.Rules[1].MinCoverageLimitExclusive", ""), F("Model.Rules[1].MaxCoverageLimitInclusive", ""),
            F("Model.Rules[1].Multiplier", "1.0"), F("Model.Rules[1].Reason", "Initial rule set"),
        ], Ct);
        var detailsUrl = saved.RequestMessage!.RequestUri!.AbsolutePath;
        await analyst.PostFormAsync(detailsUrl, "submit", [F("Submit.Comment", "Please review")], Ct);

        await File.WriteAllTextAsync(Path.Combine(outDir, "app.css"), await analyst.GetStringAsync("/app.css", Ct), Ct);
        var css = await analyst.GetStringAsync("/", Ct);
        Save(outDir, "1-home.html", await manager.GetStringAsync("/", Ct));
        Save(outDir, "2-change-request.html", await manager.GetStringAsync(detailsUrl, Ct));
        var second = await analyst.PostFormAsync("/", "create", [F("Create.Title", "Draft in progress")], Ct);
        Save(outDir, "3-edit-rules.html", await second.Content.ReadAsStringAsync(Ct));
        Assert.True(css.Length > 0);
    }

    private static void Save(string dir, string name, string html)
    {
        html = Regex.Replace(html, "href=\"[^\"]*app[^\"]*\\.css\"", "href=\"app.css\"");
        html = html.Replace("<base href=\"/\" />", "");
        File.WriteAllText(Path.Combine(dir, name), html);
    }
}
