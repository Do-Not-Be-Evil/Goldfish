using System.Net;
using System.Text.RegularExpressions;
using Goldfish.IntegrationTests.Infrastructure;
using Goldfish.Web.Security;

namespace Goldfish.IntegrationTests.Web;

/// <summary>Base for Goldfish.Web tests: an in-memory API and the web app pointed at it, on a reset database.</summary>
public abstract partial class WebTest(LocalDbFixture database) : DatabaseTest(database)
{
    private GoldfishApiFactory? _api;
    private GoldfishWebFactory? _web;

    protected GoldfishWebFactory Web => _web ?? throw new InvalidOperationException("Not initialized.");

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _api = new GoldfishApiFactory(Database.ConnectionString);
        _web = new GoldfishWebFactory(Database.ConnectionString, _api);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_web is not null) await _web.DisposeAsync();
        if (_api is not null) await _api.DisposeAsync();
        await base.DisposeAsync();
    }

    protected HttpClient Analyst() => Web.CreateClient("analyst-oid", "Ana Analyst", WebRoles.Analyst);
    protected HttpClient Manager() => Web.CreateClient("manager-oid", "Mia Manager", WebRoles.Manager);
    protected HttpClient Editor(string oid = "editor-oid") => Web.CreateClient(oid, "Eli Editor", WebRoles.PolicyEditor);

    protected static KeyValuePair<string, string> F(string name, string value) => new(name, value);

    protected static string Path(HttpResponseMessage response) => response.RequestMessage!.RequestUri!.AbsolutePath;

    protected static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"|value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        Assert.True(match.Success, $"No field {name} in page.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }
}
