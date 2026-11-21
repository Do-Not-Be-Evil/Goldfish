using Goldfish.Infrastructure.Persistence.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Goldfish.Infrastructure.Persistence.PersistenceConventions;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal sealed class PricingRuleSetVersionConfiguration : IEntityTypeConfiguration<PricingRuleSetVersionRecord>
{
    public void Configure(EntityTypeBuilder<PricingRuleSetVersionRecord> builder)
    {
        builder.ToTable("PricingRuleSetVersions", PricingSchema);

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(v => v.ChangeTicketRef).HasMaxLength(64).IsRequired();
        builder.Property(v => v.ApprovedBy).HasMaxLength(128).IsRequired();

        builder.Property(v => v.PublishedOnUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(v => v.VersionNumber)
            .IsUnique()
            .HasDatabaseName("UX_PricingRuleSetVersions_VersionNumber");

        builder.HasIndex(v => v.ChangeTicketRef)
            .IsUnique()
            .HasDatabaseName("UX_PricingRuleSetVersions_ChangeTicketRef");

        builder.HasMany(v => v.Rules)
            .WithOne()
            .HasForeignKey(r => r.VersionId)
            .HasConstraintName("FK_PricingRules_Versions")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
