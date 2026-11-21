using System.Net;
using System.Net.Http.Json;
using Goldfish.Application.Pricing.GetActiveVersion;
using Goldfish.IntegrationTests.Infrastructure;

namespace Goldfish.IntegrationTests.Api;

public sealed class ActiveVersionEndpointTests(LocalDbFixture database) : ApiTest(database)
{
    [Fact]
    public async Task No_version_is_503()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.ReaderKey);

        var response = await client.GetAsync("/api/pricing/active-version", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Shows_latest_version_and_changes_after_publish()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.ReaderKey);
        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 0, [RuleInput.CatchAll()], changeTicketRef: "CHG-1");

        var first = await client.GetFromJsonAsync<ActiveVersionStatus>("/api/pricing/active-version", Ct);

        await RuleSetPublisher.PublishAsync(Database.ConnectionString, 1, [RuleInput.CatchAll()], changeTicketRef: "CHG-2");
        var second = await client.GetFromJsonAsync<ActiveVersionStatus>("/api/pricing/active-version", Ct);

        Assert.Equal((1, "CHG-1"), (first!.VersionNumber, first.ChangeTicketRef));
        Assert.Equal((2, "CHG-2"), (second!.VersionNumber, second.ChangeTicketRef));
        Assert.Equal(DateTimeKind.Utc, second.PublishedOnUtc.Kind);
    }
}
