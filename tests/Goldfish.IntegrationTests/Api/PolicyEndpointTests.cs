using System.Net;
using System.Net.Http.Json;
using Goldfish.Application.Policies;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Api;

public sealed class PolicyEndpointTests(LocalDbFixture database) : ApiTest(database)
{
    private static HttpRequestMessage Put(Guid id, object body, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/policies/{id}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        return request;
    }

    private static readonly object NewValues = new { policyType = "Commercial", coverageLimit = 2_000_000m, basePrice = 1_300.00m };

    [Fact]
    public async Task Create_returns_201_with_etag_and_modified_by()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);

        var response = await client.PostAsJsonAsync("/api/policies",
            new { policyType = "Commercial", coverageLimit = 2_000_000m, basePrice = 1_234.57m }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var policy = (await response.Content.ReadFromJsonAsync<PolicyResponse>(Ct))!;
        Assert.Equal(policy.ETag, response.Headers.ETag!.ToString());
        Assert.Equal(GoldfishApiFactory.FullKey, policy.ModifiedBy);

        var fetched = await client.GetAsync($"/api/policies/{policy.Id}", Ct);
        Assert.Equal(policy.ETag, fetched.Headers.ETag!.ToString());
    }

    [Theory]
    [InlineData("commercial")]
    [InlineData("0")]
    [InlineData("Marine")]
    public async Task Invalid_type_is_400(string policyType)
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);

        var response = await client.PostAsJsonAsync("/api/policies",
            new { policyType, coverageLimit = 100m, basePrice = 100m }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Policy.InvalidType", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
    }

    [Fact]
    public async Task Cuj6_update_with_current_etag_records_history()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);

        var response = await client.SendAsync(Put(policy.Id, NewValues, policy.ETag), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PolicyResponse>(Ct))!;
        Assert.Equal(1_300.00m, updated.BasePrice);
        Assert.NotEqual(policy.ETag, updated.ETag);
        Assert.Equal(updated.ETag, response.Headers.ETag!.ToString());

        await using var connection = new SqlConnection(Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT BasePrice, ModifiedBy FROM dbo.PoliciesHistory WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", policy.Id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1_234.57m, reader.GetDecimal(0));
        Assert.Equal(GoldfishApiFactory.FullKey, reader.GetString(1));
    }

    [Fact]
    public async Task Cuj6_stale_etag_is_412_and_row_is_unchanged()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);
        (await client.SendAsync(Put(policy.Id, NewValues, policy.ETag), Ct)).EnsureSuccessStatusCode();

        var stale = await client.SendAsync(Put(policy.Id,
            new { policyType = "Commercial", coverageLimit = 2_000_000m, basePrice = 9_999m }, policy.ETag), Ct);

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("Policy.Concurrency", (await stale.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
        var current = await client.GetFromJsonAsync<PolicyResponse>($"/api/policies/{policy.Id}", Ct);
        Assert.Equal(1_300.00m, current!.BasePrice);
    }

    [Fact]
    public async Task Update_without_if_match_is_428()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);

        var response = await client.SendAsync(Put(policy.Id, NewValues, ifMatch: null), Ct);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task Update_unknown_policy_is_404()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);

        var response = await client.SendAsync(Put(Guid.NewGuid(), NewValues, "\"AAAAAAAAB9E=\""), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quoter_key_cannot_update_policies()
    {
        var full = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await full.CreatePolicyAsync(cancellationToken: Ct);
        var quoter = Factory.CreateClient(GoldfishApiFactory.QuoterKey);

        var response = await quoter.SendAsync(Put(policy.Id, NewValues, policy.ETag), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
