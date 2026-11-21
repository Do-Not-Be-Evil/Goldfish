using System.Net;
using System.Net.Http.Json;
using Goldfish.Application.Quotes;
using Goldfish.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Goldfish.IntegrationTests.Api;

public sealed class QuoteEndpointTests(LocalDbFixture database) : ApiTest(database)
{
    [Fact]
    public async Task Cuj1_quote_over_threshold_is_priced_and_saved()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);

        var response = await client.PostAsync($"/api/policies/{policy.Id}/quotes", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<QuoteResponse>(Ct))!;
        Assert.Equal(1, quote.VersionNumber);
        Assert.Equal("Large commercial", quote.RuleName);
        Assert.False(quote.WasCatchAll);
        Assert.Equal(1.5m, quote.Multiplier);
        Assert.Equal(1_851.855m, quote.UnroundedPrice);
        Assert.Equal(1_851.86m, quote.FinalPrice);
        Assert.EndsWith($"/api/quotes/{quote.Id}", response.Headers.Location!.ToString());

        await using var context = Database.CreateDbContext();
        var saved = await context.Quotes.SingleAsync(q => q.Id == quote.Id, Ct);
        Assert.Equal(quote.FinalPrice, saved.FinalPrice);
        Assert.Equal(quote.UnroundedPrice, saved.UnroundedPrice);
        Assert.Equal(GoldfishApiFactory.FullKey, saved.RequestedBy);
        Assert.Equal(1_234.57m, saved.BasePrice);
        Assert.Equal(2_000_000m, saved.CoverageLimit);
    }

    [Fact]
    public async Task Cuj2_boundary_value_is_priced_by_catch_all()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(coverageLimit: 1_000_000m, cancellationToken: Ct);

        var response = await client.PostAsync($"/api/policies/{policy.Id}/quotes", null, Ct);

        var quote = (await response.Content.ReadFromJsonAsync<QuoteResponse>(Ct))!;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(quote.WasCatchAll);
        Assert.Equal(1_234.57m, quote.FinalPrice);
    }

    [Fact]
    public async Task Cuj8_missing_or_unknown_key_is_401()
    {
        var anonymous = Factory.CreateClient();
        var unknown = Factory.CreateClient();
        unknown.DefaultRequestHeaders.Add("X-Api-Key", "not-a-real-key");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/policies/{Guid.NewGuid()}/quotes", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unknown.GetAsync($"/api/quotes/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/pricing/active-version", Ct)).StatusCode);
        Assert.Equal(0, await Database.CountAsync("pricing.Quotes"));
    }

    [Fact]
    public async Task Key_without_permission_is_403()
    {
        var reader = Factory.CreateClient(GoldfishApiFactory.ReaderKey);

        var response = await reader.PostAsync($"/api/policies/{Guid.NewGuid()}/quotes", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_policy_is_404()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);
        var client = Factory.CreateClient(GoldfishApiFactory.QuoterKey);

        var response = await client.PostAsync($"/api/policies/{Guid.NewGuid()}/quotes", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Policy.NotFound", problem!.Title);
    }

    [Fact]
    public async Task Cuj9_no_published_version_is_503()
    {
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);

        var response = await client.PostAsync($"/api/policies/{policy.Id}/quotes", null, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Pricing.NoActiveVersion", problem!.Title);
        Assert.Equal(0, await Database.CountAsync("pricing.Quotes"));
    }

    [Fact]
    public async Task Saved_quote_keeps_its_version_after_a_new_publish()
    {
        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString);
        var client = Factory.CreateClient(GoldfishApiFactory.FullKey);
        var policy = await client.CreatePolicyAsync(cancellationToken: Ct);
        var first = (await (await client.PostAsync($"/api/policies/{policy.Id}/quotes", null, Ct))
            .Content.ReadFromJsonAsync<QuoteResponse>(Ct))!;

        await RuleSetPublisher.PublishDefaultAsync(Database.ConnectionString, baseVersionNumber: 1, largeCommercialMultiplier: 1.6m);
        var second = (await (await client.PostAsync($"/api/policies/{policy.Id}/quotes", null, Ct))
            .Content.ReadFromJsonAsync<QuoteResponse>(Ct))!;

        var reader = Factory.CreateClient(GoldfishApiFactory.ReaderKey);
        var reread = await reader.GetFromJsonAsync<QuoteResponse>($"/api/quotes/{first.Id}", Ct);

        Assert.Equal(1, reread!.VersionNumber);
        Assert.Equal(1.5m, reread.Multiplier);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(1.6m, second.Multiplier);
    }

    [Fact]
    public async Task Unknown_quote_is_404()
    {
        var reader = Factory.CreateClient(GoldfishApiFactory.ReaderKey);

        var response = await reader.GetAsync($"/api/quotes/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
