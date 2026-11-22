namespace Goldfish.Infrastructure.Persistence.RulesAdmin;

// Persistence-only mappings for the rulesadmin schema. Every table is insert-only for the app login
// (UPDATE and DELETE are denied); rulesadmin.Approvals is written only by rulesadmin.ApproveAndPublish.

internal sealed class ChangeRequestRecord
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CreatedByOid { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
}

internal sealed class RevisionRecord
{
    public Guid Id { get; set; }
    public Guid ChangeRequestId { get; set; }
    public int RevisionNumber { get; set; }
    public int BaseVersionNumber { get; set; }
    public string AuthorOid { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
    public List<RevisionRuleRecord> Rules { get; set; } = [];
}

internal sealed class RevisionRuleRecord
{
    public Guid Id { get; set; }
    public Guid RevisionId { get; set; }
    public int Priority { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PolicyType { get; set; }
    public decimal? MinCoverageLimitExclusive { get; set; }
    public decimal? MaxCoverageLimitInclusive { get; set; }
    public decimal Multiplier { get; set; }
    public string? Reason { get; set; }
}

internal sealed class ChangeRequestEventRecord
{
    public long Id { get; set; }
    public Guid ChangeRequestId { get; set; }
    public Guid? RevisionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ActorOid { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime OccurredOnUtc { get; set; }
}

internal sealed class ApprovalRecord
{
    public Guid ChangeRequestId { get; set; }
    public Guid RevisionId { get; set; }
    public string ApproverOid { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int PublishedVersionNumber { get; set; }
    public DateTime ApprovedOnUtc { get; set; }
}

internal sealed class RatioBandRecord
{
    public int Id { get; set; }
    public decimal MinRatio { get; set; }
    public decimal MaxRatio { get; set; }
}
