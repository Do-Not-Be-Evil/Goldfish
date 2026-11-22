using Goldfish.Application.RulesAdmin;
using Goldfish.Domain.RulesAdmin;
using Goldfish.Infrastructure.Persistence.RulesAdmin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Goldfish.Infrastructure.Persistence.PersistenceConventions;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal static class RulesAdminColumns
{
    public const int ReferenceMaxLength = 64;
    public const int TitleMaxLength = 200;
    public const int CommentMaxLength = 2000;

    public static PropertyBuilder<DateTime> UtcNowDefault(this PropertyBuilder<DateTime> property) =>
        property.HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequestRecord>
{
    public void Configure(EntityTypeBuilder<ChangeRequestRecord> builder)
    {
        builder.ToTable("ChangeRequests", RulesAdminSchema, tb =>
            tb.HasCheckConstraint("CK_ChangeRequests_Reference", "[Reference] LIKE 'GFA-%'"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Reference).HasMaxLength(RulesAdminColumns.ReferenceMaxLength).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(RulesAdminColumns.TitleMaxLength).IsRequired();
        builder.Property(c => c.CreatedByOid).HasMaxLength(Actor.OidMaxLength).IsRequired();
        builder.Property(c => c.CreatedByName).HasMaxLength(Actor.NameMaxLength).IsRequired();
        builder.Property(c => c.CreatedOnUtc).UtcNowDefault();
        builder.HasIndex(c => c.Reference).IsUnique().HasDatabaseName("UX_ChangeRequests_Reference");
    }
}

internal sealed class RevisionConfiguration : IEntityTypeConfiguration<RevisionRecord>
{
    public void Configure(EntityTypeBuilder<RevisionRecord> builder)
    {
        builder.ToTable("Revisions", RulesAdminSchema, tb =>
        {
            tb.HasCheckConstraint("CK_Revisions_RevisionNumber", "[RevisionNumber] >= 1");
            tb.HasCheckConstraint("CK_Revisions_BaseVersionNumber", "[BaseVersionNumber] >= 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(r => r.AuthorOid).HasMaxLength(Actor.OidMaxLength).IsRequired();
        builder.Property(r => r.AuthorName).HasMaxLength(Actor.NameMaxLength).IsRequired();
        builder.Property(r => r.CreatedOnUtc).UtcNowDefault();

        // Two people saving from the same revision both pick number N+1; the second insert fails (2601).
        builder.HasIndex(r => new { r.ChangeRequestId, r.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("UX_Revisions_ChangeRequest_Number");

        builder.HasOne<ChangeRequestRecord>().WithMany().HasForeignKey(r => r.ChangeRequestId)
            .HasConstraintName("FK_Revisions_ChangeRequests").OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Rules).WithOne().HasForeignKey(r => r.RevisionId)
            .HasConstraintName("FK_RevisionRules_Revisions").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Columns mirror pricing.PricingRuleInput exactly so a stored revision publishes without conversion.</summary>
internal sealed class RevisionRuleConfiguration : IEntityTypeConfiguration<RevisionRuleRecord>
{
    public void Configure(EntityTypeBuilder<RevisionRuleRecord> builder)
    {
        builder.ToTable("RevisionRules", RulesAdminSchema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(r => r.Name).HasMaxLength(RuleNameMaxLength).IsRequired();
        builder.Property(r => r.PolicyType).HasMaxLength(PolicyTypeMaxLength).UseCollation(BinaryCollation);
        builder.Property(r => r.MinCoverageLimitExclusive).HasPrecision(18, 2);
        builder.Property(r => r.MaxCoverageLimitInclusive).HasPrecision(18, 2);
        builder.Property(r => r.Multiplier).HasPrecision(9, 4);
        builder.Property(r => r.Reason).HasMaxLength(RuleSetValidator.ReasonMaxLength);

        builder.HasIndex(r => new { r.RevisionId, r.Name }).IsUnique().HasDatabaseName("UX_RevisionRules_Revision_Name");
    }
}

internal sealed class ChangeRequestEventConfiguration : IEntityTypeConfiguration<ChangeRequestEventRecord>
{
    public void Configure(EntityTypeBuilder<ChangeRequestEventRecord> builder)
    {
        var types = string.Join(",", Enum.GetNames<ChangeRequestEventType>().Select(n => $"'{n}'"));
        builder.ToTable("ChangeRequestEvents", RulesAdminSchema, tb =>
            tb.HasCheckConstraint("CK_ChangeRequestEvents_Type", $"[Type] IN ({types})"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Type).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ActorOid).HasMaxLength(Actor.OidMaxLength).IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(Actor.NameMaxLength).IsRequired();
        builder.Property(e => e.Comment).HasMaxLength(RulesAdminColumns.CommentMaxLength);
        builder.Property(e => e.OccurredOnUtc).UtcNowDefault();

        builder.HasIndex(e => new { e.ChangeRequestId, e.Id }).HasDatabaseName("IX_ChangeRequestEvents_ChangeRequest");

        builder.HasOne<ChangeRequestRecord>().WithMany().HasForeignKey(e => e.ChangeRequestId)
            .HasConstraintName("FK_ChangeRequestEvents_ChangeRequests").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RevisionRecord>().WithMany().HasForeignKey(e => e.RevisionId)
            .HasConstraintName("FK_ChangeRequestEvents_Revisions").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<ApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ApprovalRecord> builder)
    {
        // One approval per change request: the primary key makes a second, concurrent approval fail.
        builder.ToTable("Approvals", RulesAdminSchema);
        builder.HasKey(a => a.ChangeRequestId);
        builder.Property(a => a.ApproverOid).HasMaxLength(Actor.OidMaxLength).IsRequired();
        builder.Property(a => a.ApproverName).HasMaxLength(Actor.NameMaxLength).IsRequired();
        builder.Property(a => a.Comment).HasMaxLength(RulesAdminColumns.CommentMaxLength);
        builder.Property(a => a.ApprovedOnUtc).UtcNowDefault();

        builder.HasIndex(a => a.PublishedVersionNumber).IsUnique().HasDatabaseName("UX_Approvals_PublishedVersionNumber");

        builder.HasOne<ChangeRequestRecord>().WithOne().HasForeignKey<ApprovalRecord>(a => a.ChangeRequestId)
            .HasConstraintName("FK_Approvals_ChangeRequests").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RevisionRecord>().WithMany().HasForeignKey(a => a.RevisionId)
            .HasConstraintName("FK_Approvals_Revisions").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RatioBandConfiguration : IEntityTypeConfiguration<RatioBandRecord>
{
    public void Configure(EntityTypeBuilder<RatioBandRecord> builder)
    {
        builder.ToTable("RatioBand", RulesAdminSchema, tb =>
        {
            tb.HasCheckConstraint("CK_RatioBand_SingleRow", "[Id] = 1");
            tb.HasCheckConstraint("CK_RatioBand_Range", "[MinRatio] > 0 AND [MinRatio] <= 1 AND [MaxRatio] >= 1");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.MinRatio).HasPrecision(9, 4);
        builder.Property(b => b.MaxRatio).HasPrecision(9, 4);
        builder.HasData(new RatioBandRecord { Id = 1, MinRatio = RatioBand.Default.MinRatio, MaxRatio = RatioBand.Default.MaxRatio });
    }
}
