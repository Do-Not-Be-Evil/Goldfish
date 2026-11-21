using Goldfish.Domain.Policies;
using Goldfish.Domain.Quotes;
using Goldfish.Infrastructure.Persistence.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Goldfish.Infrastructure.Persistence.PersistenceConventions;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes", PricingSchema, tb =>
            tb.HasCheckConstraint("CK_Quotes_PolicyType", $"[PolicyType] IN ({PolicyTypeList})"));

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.RuleName).HasMaxLength(RuleNameMaxLength).IsRequired();
        builder.Property(q => q.PolicyType)
            .HasConversion<string>()
            .HasMaxLength(PolicyTypeMaxLength)
            .UseCollation(BinaryCollation)
            .IsRequired();
        builder.Property(q => q.CoverageLimit).HasPrecision(18, 2);
        builder.Property(q => q.BasePrice).HasPrecision(18, 2);
        builder.Property(q => q.Multiplier).HasPrecision(9, 4);
        builder.Property(q => q.UnroundedPrice).HasPrecision(28, 6);
        builder.Property(q => q.FinalPrice).HasPrecision(18, 2);
        builder.Property(q => q.RequestedBy).HasMaxLength(CallerNameMaxLength).IsRequired();
        builder.Property(q => q.CreatedOnUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(q => q.PolicyId)
            .HasConstraintName("FK_Quotes_Policies")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PricingRuleSetVersionRecord>()
            .WithMany()
            .HasForeignKey(q => q.VersionId)
            .HasConstraintName("FK_Quotes_Versions")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PricingRuleRecord>()
            .WithMany()
            .HasForeignKey(q => new { q.VersionId, q.RuleId })
            .HasPrincipalKey(r => new { r.VersionId, r.Id })
            .HasConstraintName("FK_Quotes_Rules")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => q.PolicyId).HasDatabaseName("IX_Quotes_PolicyId");
    }
}
