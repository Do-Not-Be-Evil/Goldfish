using System.Net;
using System.Net.Http.Json;
using Goldfish.Application.Policies;
using Goldfish.Application.Policies.ListPolicies;
using Goldfish.IntegrationTests.Infrastructure;

namespace Goldfish.IntegrationTests.Api;

public sealed class PolicyListEndpointTests(LocalDbFixture database) : ApiTest(database)
{
    private async Task CreateAsync(HttpClient client, string type, decimal coverage)
    {
        var response = await client.PostAsJsonAsync("/api/policies", new { policyType = type, coverageLimit = coverage, basePrice = 100m }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Lists_policies_newest_first_with_paging_and_a_type_filter()
    {
        var full = Factory.CreateClient(GoldfishApiFactory.FullKey);
        await CreateAsync(full, "Commercial", 1_000m);
        await CreateAsync(full, "Personal", 2_000m);
        await CreateAsync(full, "Commercial", 3_000m);

        var reader = Factory.CreateClient(GoldfishApiFactory.ReaderKey);
        var page = await reader.GetFromJsonAsync<PolicyPage>("/api/policies?skip=0&take=2", Ct);
        var commercial = await reader.GetFromJsonAsync<PolicyPage>("/api/policies?policyType=Commercial", Ct);

        Assert.Equal(3, page!.Total);
        Assert.Equal([3_000m, 2_000m], page.Items.Select(p => p.CoverageLimit));
        Assert.All(page.Items, p => Assert.False(string.IsNullOrEmpty(p.ETag)));
        Assert.Equal(2, commercial!.Total);
        Assert.All(commercial.Items, p => Assert.Equal("Commercial", p.PolicyType));
    }

    [Theory]
    [InlineData("/api/policies?take=0")]
    [InlineData("/api/policies?take=201")]
    [InlineData("/api/policies?skip=-1")]
    [InlineData("/api/policies?policyType=commercial")]
    public async Task Bad_paging_or_type_is_400(string url)
    {
        var response = await Factory.CreateClient(GoldfishApiFactory.ReaderKey).GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Listing_needs_policies_read()
    {
        var response = await Factory.CreateClient(GoldfishApiFactory.QuoterKey).GetAsync("/api/policies", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
