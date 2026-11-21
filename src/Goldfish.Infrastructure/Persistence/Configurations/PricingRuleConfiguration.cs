using Goldfish.Infrastructure.Persistence.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Goldfish.Infrastructure.Persistence.PersistenceConventions;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRuleRecord>
{
    public const string CatchAllFilter =
        "[PolicyType] IS NULL AND [MinCoverageLimitExclusive] IS NULL AND [MaxCoverageLimitInclusive] IS NULL";

    public void Configure(EntityTypeBuilder<PricingRuleRecord> builder)
    {
        builder.ToTable("PricingRules", PricingSchema, tb =>
        {
            tb.HasCheckConstraint("CK_PricingRules_Priority_Min", "[Priority] >= 1");
            tb.HasCheckConstraint("CK_PricingRules_Multiplier_Positive", "[Multiplier] > 0");
            tb.HasCheckConstraint("CK_PricingRules_PolicyType", $"[PolicyType] IS NULL OR [PolicyType] IN ({PolicyTypeList})");
            tb.HasCheckConstraint("CK_PricingRules_CoverageRange",
                "[MinCoverageLimitExclusive] IS NULL OR [MaxCoverageLimitInclusive] IS NULL OR [MinCoverageLimitExclusive] < [MaxCoverageLimitInclusive]");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Lets pricing.Quotes reference (VersionId, RuleId) so a quote's rule must belong to its version.
        builder.HasAlternateKey(r => new { r.VersionId, r.Id }).HasName("UX_PricingRules_Version_Id");

        builder.Property(r => r.Name).HasMaxLength(RuleNameMaxLength).IsRequired();
        builder.Property(r => r.PolicyType).HasMaxLength(PolicyTypeMaxLength).UseCollation(BinaryCollation);
        builder.Property(r => r.MinCoverageLimitExclusive).HasPrecision(18, 2);
        builder.Property(r => r.MaxCoverageLimitInclusive).HasPrecision(18, 2);
        builder.Property(r => r.Multiplier).HasPrecision(9, 4);

        builder.HasIndex(r => new { r.VersionId, r.Priority })
            .IsUnique()
            .HasDatabaseName("UX_PricingRules_Version_Priority");

        builder.HasIndex(r => new { r.VersionId, r.Name })
            .IsUnique()
            .HasDatabaseName("UX_PricingRules_Version_Name");

        builder.HasIndex(r => r.VersionId)
            .IsUnique()
            .HasFilter(CatchAllFilter)
            .HasDatabaseName("UX_PricingRules_Version_CatchAll");
    }
}
