using System.Net;
using System.Text.Json;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Goldfish.IntegrationTests.Api;

public sealed class SwaggerTests(LocalDbFixture database) : ApiTest(database)
{
    private HttpClient CreateDevelopmentClient() =>
        Factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

    [Fact]
    public async Task Swagger_ui_is_served_anonymously_in_development()
    {
        var response = await CreateDevelopmentClient().GetAsync("/swagger/index.html", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ui_is_not_served_outside_development()
    {
        var response = await Factory.CreateClient().GetAsync("/swagger/index.html", Ct);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Document_declares_api_key_and_secures_every_operation()
    {
        var json = await CreateDevelopmentClient().GetStringAsync("/openapi/v1.json", Ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("ApiKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Api-Key", scheme.GetProperty("name").GetString());

        var operations = root.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(op => (Name: $"{op.Name} {path.Name}", Op: op.Value)))
            .ToList();

        Assert.NotEmpty(operations);
        Assert.All(operations, o =>
        {
            Assert.True(o.Op.TryGetProperty("security", out var security), $"{o.Name} has no security requirement.");
            Assert.Contains(security.EnumerateArray(), r => r.TryGetProperty("ApiKey", out _));
        });
    }
}
