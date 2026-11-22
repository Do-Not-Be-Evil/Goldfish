using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Goldfish.Web.Api;

/// <summary>
/// Where the Goldfish API is and the API key this app uses. The key needs policies:read, policies:write and
/// pricing:read. The API records the key's name as each policy's ModifiedBy; the app logs the signed-in person.
/// </summary>
public sealed class GoldfishApiOptions
{
    public const string SectionName = "GoldfishApi";

    [Required, Url] public string BaseUrl { get; set; } = string.Empty;
    [Required] public string ApiKey { get; set; } = "1234554321";
}

public sealed record ApiPolicy(
    Guid Id,
    string PolicyType,
    decimal CoverageLimit,
    decimal BasePrice,
    DateTime CreatedOnUtc,
    string ModifiedBy,
    string ETag);

public sealed record ApiPolicyPage(IReadOnlyList<ApiPolicy> Items, int Total, int Skip, int Take);

public sealed record ApiActiveVersion(int VersionNumber, string ChangeTicketRef, DateTime PublishedOnUtc);

public sealed record PolicyInput(string PolicyType, decimal CoverageLimit, decimal BasePrice);

/// <summary>An RFC 7807 problem returned by the API; Title is the error code, such as Policy.Concurrency.</summary>
public sealed record ApiProblem(int Status, string? Title, string? Detail);

public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiProblem? problem) => (Value, Problem) = (value, problem);

    public T? Value { get; }
    public ApiProblem? Problem { get; }
    public bool IsSuccess => Problem is null;

    public static ApiResult<T> Ok(T value) => new(value, null);
    public static ApiResult<T> Fail(ApiProblem problem) => new(default, problem);
}

/// <summary>Typed client for the Goldfish API's policy and pricing endpoints.</summary>
public sealed class GoldfishApiClient(HttpClient http)
{
    public const string ApiKeyHeader = "X-Api-Key";

    public Task<ApiResult<ApiPolicyPage>> ListPoliciesAsync(int skip, int take, string? policyType, CancellationToken cancellationToken)
    {
        var url = $"api/policies?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(policyType))
        {
            url += $"&policyType={Uri.EscapeDataString(policyType)}";
        }

        return SendAsync<ApiPolicyPage>(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
    }

    public Task<ApiResult<ApiPolicy>> GetPolicyAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<ApiPolicy>(new HttpRequestMessage(HttpMethod.Get, $"api/policies/{id}"), cancellationToken);

    public Task<ApiResult<ApiPolicy>> CreatePolicyAsync(PolicyInput input, CancellationToken cancellationToken) =>
        SendAsync<ApiPolicy>(new HttpRequestMessage(HttpMethod.Post, "api/policies") { Content = JsonContent.Create(input) },
            cancellationToken);

    /// <summary>Updates a policy only if it still has <paramref name="etag"/>; otherwise the API returns 412.</summary>
    public Task<ApiResult<ApiPolicy>> UpdatePolicyAsync(Guid id, string etag, PolicyInput input, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/policies/{id}") { Content = JsonContent.Create(input) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return SendAsync<ApiPolicy>(request, cancellationToken);
    }

    public Task<ApiResult<ApiActiveVersion>> GetActiveVersionAsync(CancellationToken cancellationToken) =>
        SendAsync<ApiActiveVersion>(new HttpRequestMessage(HttpMethod.Get, "api/pricing/active-version"), cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new InvalidOperationException($"The API returned an empty body for {request.RequestUri}.");
            return ApiResult<T>.Ok(value);
        }

        ApiProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
        }

        return ApiResult<T>.Fail(problem is null
            ? new ApiProblem((int)response.StatusCode, response.StatusCode.ToString(), null)
            : problem with { Status = (int)response.StatusCode });
    }
}

public static class ApiStatus
{
    public static bool Is(this ApiProblem? problem, HttpStatusCode status) => problem?.Status == (int)status;
}
