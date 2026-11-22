using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Goldfish.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRulesAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Products sample was removed from the code in commit 9b87966; this drops its table.
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.EnsureSchema(
                name: "rulesadmin");

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                schema: "rulesadmin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByOid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.CheckConstraint("CK_ChangeRequests_Reference", "[Reference] LIKE 'GFA-%'");
                });

            migrationBuilder.CreateTable(
                name: "RatioBand",
                schema: "rulesadmin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    MinRatio = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    MaxRatio = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatioBand", x => x.Id);
                    table.CheckConstraint("CK_RatioBand_Range", "[MinRatio] > 0 AND [MinRatio] <= 1 AND [MaxRatio] >= 1");
                    table.CheckConstraint("CK_RatioBand_SingleRow", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "Revisions",
                schema: "rulesadmin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    BaseVersionNumber = table.Column<int>(type: "int", nullable: false),
                    AuthorOid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisions", x => x.Id);
                    table.CheckConstraint("CK_Revisions_BaseVersionNumber", "[BaseVersionNumber] >= 0");
                    table.CheckConstraint("CK_Revisions_RevisionNumber", "[RevisionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_Revisions_ChangeRequests",
                        column: x => x.ChangeRequestId,
                        principalSchema: "rulesadmin",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "rulesadmin",
                columns: table => new
                {
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverOid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublishedVersionNumber = table.Column<int>(type: "int", nullable: false),
                    ApprovedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.ChangeRequestId);
                    table.ForeignKey(
                        name: "FK_Approvals_ChangeRequests",
                        column: x => x.ChangeRequestId,
                        principalSchema: "rulesadmin",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Approvals_Revisions",
                        column: x => x.RevisionId,
                        principalSchema: "rulesadmin",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeRequestEvents",
                schema: "rulesadmin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActorOid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequestEvents", x => x.Id);
                    table.CheckConstraint("CK_ChangeRequestEvents_Type", "[Type] IN ('Submitted','Rejected','Withdrawn','Comment')");
                    table.ForeignKey(
                        name: "FK_ChangeRequestEvents_ChangeRequests",
                        column: x => x.ChangeRequestId,
                        principalSchema: "rulesadmin",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeRequestEvents_Revisions",
                        column: x => x.RevisionId,
                        principalSchema: "rulesadmin",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevisionRules",
                schema: "rulesadmin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_BIN2"),
                    MinCoverageLimitExclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxCoverageLimitInclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Multiplier = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionRules_Revisions",
                        column: x => x.RevisionId,
                        principalSchema: "rulesadmin",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "rulesadmin",
                table: "RatioBand",
                columns: new[] { "Id", "MaxRatio", "MinRatio" },
                values: new object[] { 1, 1.25m, 0.8m });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RevisionId",
                schema: "rulesadmin",
                table: "Approvals",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "UX_Approvals_PublishedVersionNumber",
                schema: "rulesadmin",
                table: "Approvals",
                column: "PublishedVersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequestEvents_ChangeRequest",
                schema: "rulesadmin",
                table: "ChangeRequestEvents",
                columns: new[] { "ChangeRequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequestEvents_RevisionId",
                schema: "rulesadmin",
                table: "ChangeRequestEvents",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "UX_ChangeRequests_Reference",
                schema: "rulesadmin",
                table: "ChangeRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RevisionRules_Revision_Name",
                schema: "rulesadmin",
                table: "RevisionRules",
                columns: new[] { "RevisionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Revisions_ChangeRequest_Number",
                schema: "rulesadmin",
                table: "Revisions",
                columns: new[] { "ChangeRequestId", "RevisionNumber" },
                unique: true);

            // Ownership chaining from the wrapper into pricing.PublishRuleSetVersion needs both schemas owned by dbo.
            migrationBuilder.Sql("ALTER AUTHORIZATION ON SCHEMA::rulesadmin TO dbo;");
            migrationBuilder.Sql(CreateApproveProcedure);
        }

        // The only route to publish for the rules admin app. It re-checks every approval rule against stored,
        // insert-only rows, then publishes the stored revision and records the approval in one transaction.
        // Errors 51001-51007 are listed in docs/pricing-rules.md. Never call the publish procedure with
        // @DryRun = 1 from here: its ROLLBACK would end this procedure's transaction.
        private const string CreateApproveProcedure = """
            CREATE PROCEDURE rulesadmin.ApproveAndPublish
                @ChangeRequestId  uniqueidentifier,
                @RevisionId       uniqueidentifier,
                @ApproverOid      nvarchar(64),
                @ApproverName     nvarchar(256),
                @Comment          nvarchar(2000) = NULL,
                @NewVersionNumber int OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF NULLIF(LTRIM(RTRIM(@ApproverOid)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(@ApproverName)), N'') IS NULL
                    THROW 51001, N'The approver''s object ID and name are required.', 1;

                BEGIN TRANSACTION;

                -- Serializes approvals of one request: a second approver waits here, then fails on 51003.
                DECLARE @Reference nvarchar(64);
                SELECT @Reference = Reference
                FROM rulesadmin.ChangeRequests WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @ChangeRequestId;

                IF @Reference IS NULL
                    THROW 51002, N'The change request does not exist.', 1;

                IF EXISTS (SELECT 1 FROM rulesadmin.Approvals WHERE ChangeRequestId = @ChangeRequestId)
                    THROW 51003, N'The change request has already been published.', 1;

                DECLARE @LatestRevisionId uniqueidentifier, @BaseVersionNumber int;
                SELECT TOP (1) @LatestRevisionId = Id, @BaseVersionNumber = BaseVersionNumber
                FROM rulesadmin.Revisions
                WHERE ChangeRequestId = @ChangeRequestId
                ORDER BY RevisionNumber DESC;

                IF @LatestRevisionId IS NULL OR @LatestRevisionId <> @RevisionId
                    THROW 51004, N'The reviewed revision is not the latest revision of the change request.', 1;

                DECLARE @LastType nvarchar(16), @LastRevisionId uniqueidentifier;
                SELECT TOP (1) @LastType = Type, @LastRevisionId = RevisionId
                FROM rulesadmin.ChangeRequestEvents
                WHERE ChangeRequestId = @ChangeRequestId AND Type <> N'Comment'
                ORDER BY Id DESC;

                IF @LastType IS NULL OR @LastType <> N'Submitted' OR @LastRevisionId <> @RevisionId
                    THROW 51005, N'The latest revision has not been submitted for approval.', 1;

                IF EXISTS (SELECT 1 FROM rulesadmin.ChangeRequests WHERE Id = @ChangeRequestId AND CreatedByOid = @ApproverOid)
                   OR EXISTS (SELECT 1 FROM rulesadmin.Revisions WHERE ChangeRequestId = @ChangeRequestId AND AuthorOid = @ApproverOid)
                   OR EXISTS (SELECT 1 FROM rulesadmin.ChangeRequestEvents
                              WHERE ChangeRequestId = @ChangeRequestId AND Type = N'Submitted' AND ActorOid = @ApproverOid)
                    THROW 51006, N'A person who created, edited or submitted the change request cannot approve it.', 1;

                -- Same rule as RuleSetDiff.Compute: a new rule, or a ratio outside the band, needs a reason.
                DECLARE @MinRatio decimal(9,4), @MaxRatio decimal(9,4);
                SELECT @MinRatio = MinRatio, @MaxRatio = MaxRatio FROM rulesadmin.RatioBand WHERE Id = 1;

                IF EXISTS (
                    SELECT 1
                    FROM rulesadmin.RevisionRules n
                    LEFT JOIN (
                        SELECT r.Name, r.Multiplier
                        FROM pricing.PricingRules r
                        JOIN pricing.PricingRuleSetVersions v ON v.Id = r.VersionId
                        WHERE v.VersionNumber = @BaseVersionNumber
                    ) o ON o.Name = n.Name
                    WHERE n.RevisionId = @RevisionId
                      AND NULLIF(LTRIM(RTRIM(n.Reason)), N'') IS NULL
                      AND (o.Name IS NULL
                           OR CAST(n.Multiplier / o.Multiplier AS decimal(19,4)) NOT BETWEEN @MinRatio AND @MaxRatio))
                    THROW 51007, N'A rule that is new, or whose multiplier ratio is outside the band, has no reason.', 1;

                DECLARE @Rules pricing.PricingRuleInput;
                INSERT @Rules (Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
                SELECT Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier
                FROM rulesadmin.RevisionRules
                WHERE RevisionId = @RevisionId;

                DECLARE @ApprovedBy nvarchar(128) = LEFT(@ApproverOid + N'|' + @ApproverName, 128);

                -- Raises 50003 (or 2601) when another version was published after this revision's base.
                EXEC pricing.PublishRuleSetVersion
                    @ChangeTicketRef   = @Reference,
                    @ApprovedBy        = @ApprovedBy,
                    @BaseVersionNumber = @BaseVersionNumber,
                    @DryRun            = 0,
                    @Rules             = @Rules,
                    @NewVersionNumber  = @NewVersionNumber OUTPUT;

                INSERT rulesadmin.Approvals (ChangeRequestId, RevisionId, ApproverOid, ApproverName, Comment, PublishedVersionNumber)
                VALUES (@ChangeRequestId, @RevisionId, @ApproverOid, @ApproverName, NULLIF(LTRIM(RTRIM(@Comment)), N''), @NewVersionNumber);

                COMMIT TRANSACTION;
            END;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE rulesadmin.ApproveAndPublish;");

            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "rulesadmin");

            migrationBuilder.DropTable(
                name: "ChangeRequestEvents",
                schema: "rulesadmin");

            migrationBuilder.DropTable(
                name: "RatioBand",
                schema: "rulesadmin");

            migrationBuilder.DropTable(
                name: "RevisionRules",
                schema: "rulesadmin");

            migrationBuilder.DropTable(
                name: "Revisions",
                schema: "rulesadmin");

            migrationBuilder.DropTable(
                name: "ChangeRequests",
                schema: "rulesadmin");

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });
        }
    }
}
