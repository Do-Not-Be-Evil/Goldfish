namespace Goldfish.Api.Authentication;

/// <summary>Bound from the "ApiKeys" configuration section. Only SHA-256 hashes of keys are stored.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    public List<ApiKeyEntry> Keys { get; set; } = new();

    /// <summary>Startup validation: unique 1-64 character names, 64-hex-character hashes, known permissions.</summary>
    public bool IsValid()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in Keys)
        {
            if (string.IsNullOrWhiteSpace(key.Name) || key.Name.Length > 64 || !names.Add(key.Name))
            {
                return false;
            }

            if (key.KeyHash is not { Length: 64 } || !key.KeyHash.All(Uri.IsHexDigit))
            {
                return false;
            }

            if (key.Permissions.Any(p => !Permissions.All.Contains(p)))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class ApiKeyEntry
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = [];
}
