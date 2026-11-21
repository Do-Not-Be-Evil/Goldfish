using Goldfish.Domain.Policies;

namespace Goldfish.Application.Policies;

public sealed record PolicyResponse(
    Guid Id,
    string PolicyType,
    decimal CoverageLimit,
    decimal BasePrice,
    DateTime CreatedOnUtc,
    string ModifiedBy,
    string ETag)
{
    internal static PolicyResponse FromEntity(Policy policy) =>
        new(policy.Id,
            policy.Type.ToString(),
            policy.CoverageLimit,
            policy.BasePrice,
            policy.CreatedOnUtc,
            policy.ModifiedBy,
            PolicyETag.Format(policy.RowVersion));
}

/// <summary>An ETag is the policy's rowversion, base64-encoded and quoted.</summary>
public static class PolicyETag
{
    public static string Format(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    public static bool TryParse(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            return false; // weak ETags are not accepted for If-Match
        }

        trimmed = trimmed.Trim('"');
        try
        {
            rowVersion = Convert.FromBase64String(trimmed);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
