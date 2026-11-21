namespace Goldfish.Infrastructure.Persistence;

internal static class PersistenceConventions
{
    public const string PricingSchema = "pricing";
    public const string BinaryCollation = "Latin1_General_BIN2";
    public const int PolicyTypeMaxLength = 32;
    public const int CallerNameMaxLength = 64;
    public const int RuleNameMaxLength = 200;

    /// <summary>SQL list matching <see cref="Domain.Policies.PolicyType"/>.</summary>
    public const string PolicyTypeList = "'Commercial','Personal'";
}
