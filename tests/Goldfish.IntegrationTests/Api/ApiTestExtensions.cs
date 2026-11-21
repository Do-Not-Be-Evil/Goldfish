using System.Net.Http.Json;
using Goldfish.Application.Policies;

namespace Goldfish.IntegrationTests.Api;

internal static class ApiTestExtensions
{
    public static async Task<PolicyResponse> CreatePolicyAsync(
        this HttpClient client,
        string policyType = "Commercial",
        decimal coverageLimit = 2_000_000m,
        decimal basePrice = 1_234.57m,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync("/api/policies",
            new { policyType, coverageLimit, basePrice }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PolicyResponse>(cancellationToken))!;
    }
}
