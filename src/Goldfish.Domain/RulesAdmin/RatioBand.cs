namespace Goldfish.Domain.RulesAdmin;

/// <summary>
/// The range of new/old multiplier ratios that may be published without a written reason.
/// Stored in rulesadmin.RatioBand and enforced again by rulesadmin.ApproveAndPublish.
/// </summary>
public sealed record RatioBand(decimal MinRatio, decimal MaxRatio)
{
    public static readonly RatioBand Default = new(0.8m, 1.25m);

    public bool Contains(decimal ratio) => ratio >= MinRatio && ratio <= MaxRatio;
}
