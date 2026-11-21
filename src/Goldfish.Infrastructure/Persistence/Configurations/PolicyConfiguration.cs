using Goldfish.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Goldfish.Infrastructure.Persistence.PersistenceConventions;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies", tb =>
        {
            tb.IsTemporal(t =>
            {
                t.HasPeriodStart("PeriodStart");
                t.HasPeriodEnd("PeriodEnd");
                t.UseHistoryTable("PoliciesHistory");
            });
            tb.HasCheckConstraint("CK_Policies_PolicyType", $"[PolicyType] IN ({PolicyTypeList})");
            tb.HasCheckConstraint("CK_Policies_CoverageLimit_Positive", "[CoverageLimit] > 0");
            tb.HasCheckConstraint("CK_Policies_BasePrice_Positive", "[BasePrice] > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Type)
            .HasColumnName("PolicyType")
            .HasConversion<string>()
            .HasMaxLength(PolicyTypeMaxLength)
            .UseCollation(BinaryCollation)
            .IsRequired();

        builder.Property(p => p.CoverageLimit).HasPrecision(18, 2);
        builder.Property(p => p.BasePrice).HasPrecision(18, 2);

        builder.Property(p => p.ModifiedBy)
            .HasMaxLength(CallerNameMaxLength)
            .IsRequired();

        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Property(p => p.CreatedOnUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
