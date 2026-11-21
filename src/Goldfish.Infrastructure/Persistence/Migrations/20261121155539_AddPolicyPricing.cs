using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Goldfish.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_BIN2"),
                    CoverageLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.CheckConstraint("CK_Policies_BasePrice_Positive", "[BasePrice] > 0");
                    table.CheckConstraint("CK_Policies_CoverageLimit_Positive", "[CoverageLimit] > 0");
                    table.CheckConstraint("CK_Policies_PolicyType", "[PolicyType] IN ('Commercial','Personal')");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PoliciesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "PricingRuleSetVersions",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ChangeTicketRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PublishedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRuleSetVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingRules",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_BIN2"),
                    MinCoverageLimitExclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxCoverageLimitInclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Multiplier = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                    table.UniqueConstraint("UX_PricingRules_Version_Id", x => new { x.VersionId, x.Id });
                    table.CheckConstraint("CK_PricingRules_CoverageRange", "[MinCoverageLimitExclusive] IS NULL OR [MaxCoverageLimitInclusive] IS NULL OR [MinCoverageLimitExclusive] < [MaxCoverageLimitInclusive]");
                    table.CheckConstraint("CK_PricingRules_Multiplier_Positive", "[Multiplier] > 0");
                    table.CheckConstraint("CK_PricingRules_PolicyType", "[PolicyType] IS NULL OR [PolicyType] IN ('Commercial','Personal')");
                    table.CheckConstraint("CK_PricingRules_Priority_Min", "[Priority] >= 1");
                    table.ForeignKey(
                        name: "FK_PricingRules_Versions",
                        column: x => x.VersionId,
                        principalSchema: "pricing",
                        principalTable: "PricingRuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WasCatchAll = table.Column<bool>(type: "bit", nullable: false),
                    PolicyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_BIN2"),
                    CoverageLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    UnroundedPrice = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.CheckConstraint("CK_Quotes_PolicyType", "[PolicyType] IN ('Commercial','Personal')");
                    table.ForeignKey(
                        name: "FK_Quotes_Policies",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Rules",
                        columns: x => new { x.VersionId, x.RuleId },
                        principalSchema: "pricing",
                        principalTable: "PricingRules",
                        principalColumns: new[] { "VersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Versions",
                        column: x => x.VersionId,
                        principalSchema: "pricing",
                        principalTable: "PricingRuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PricingRules_Version_CatchAll",
                schema: "pricing",
                table: "PricingRules",
                column: "VersionId",
                unique: true,
                filter: "[PolicyType] IS NULL AND [MinCoverageLimitExclusive] IS NULL AND [MaxCoverageLimitInclusive] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PricingRules_Version_Name",
                schema: "pricing",
                table: "PricingRules",
                columns: new[] { "VersionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PricingRules_Version_Priority",
                schema: "pricing",
                table: "PricingRules",
                columns: new[] { "VersionId", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PricingRuleSetVersions_ChangeTicketRef",
                schema: "pricing",
                table: "PricingRuleSetVersions",
                column: "ChangeTicketRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PricingRuleSetVersions_VersionNumber",
                schema: "pricing",
                table: "PricingRuleSetVersions",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_PolicyId",
                schema: "pricing",
                table: "Quotes",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_VersionId_RuleId",
                schema: "pricing",
                table: "Quotes",
                columns: new[] { "VersionId", "RuleId" });

            migrationBuilder.Sql(CreateRuleInputType);
            migrationBuilder.Sql(CreatePublishProcedure);
        }

        // Table-valued parameter for pricing.PublishRuleSetVersion. Column order is part of the contract.
        private const string CreateRuleInputType = """
            CREATE TYPE pricing.PricingRuleInput AS TABLE
            (
                Priority                  int            NOT NULL,
                Name                      nvarchar(200)  NOT NULL,
                PolicyType                nvarchar(32)   COLLATE Latin1_General_BIN2 NULL,
                MinCoverageLimitExclusive decimal(18,2)  NULL,
                MaxCoverageLimitInclusive decimal(18,2)  NULL,
                Multiplier                decimal(9,4)   NOT NULL
            );
            """;

        // The only way rule rows are written. See "Publishing a version" in docs/designs/policy-pricing-rules.md
        // for the steps and the error table (50001-50005 here; 547/2601 come from named constraints).
        private const string CreatePublishProcedure = """
            CREATE PROCEDURE pricing.PublishRuleSetVersion
                @ChangeTicketRef   nvarchar(64),
                @ApprovedBy        nvarchar(128),
                @BaseVersionNumber int,
                @DryRun            bit = 0,
                @Rules             pricing.PricingRuleInput READONLY,
                @NewVersionNumber  int OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF NULLIF(LTRIM(RTRIM(@ChangeTicketRef)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(@ApprovedBy)), N'') IS NULL
                    THROW 50001, N'ChangeTicketRef and ApprovedBy are required.', 1;

                IF NOT EXISTS (SELECT 1 FROM @Rules)
                    THROW 50002, N'The rule set is empty.', 1;

                DECLARE @Latest int = ISNULL((SELECT MAX(VersionNumber) FROM pricing.PricingRuleSetVersions), 0);
                IF @BaseVersionNumber IS NULL OR @BaseVersionNumber <> @Latest
                BEGIN
                    DECLARE @StaleMessage nvarchar(400) = CONCAT(
                        N'Base version ', ISNULL(CONVERT(nvarchar(11), @BaseVersionNumber), N'NULL'),
                        N' is not the latest; rebuild the script from version ', @Latest, N'.');
                    THROW 50003, @StaleMessage, 1;
                END;

                IF (SELECT COUNT(*) FROM @Rules
                    WHERE PolicyType IS NULL AND MinCoverageLimitExclusive IS NULL AND MaxCoverageLimitInclusive IS NULL) <> 1
                    THROW 50004, N'The rule set must contain exactly one catch-all rule (no conditions).', 1;

                IF EXISTS (
                    SELECT 1
                    FROM @Rules c
                    JOIN @Rules r ON r.Priority >= c.Priority
                    WHERE c.PolicyType IS NULL AND c.MinCoverageLimitExclusive IS NULL AND c.MaxCoverageLimitInclusive IS NULL
                      AND NOT (r.PolicyType IS NULL AND r.MinCoverageLimitExclusive IS NULL AND r.MaxCoverageLimitInclusive IS NULL))
                    THROW 50005, N'The catch-all rule must have a larger priority number than every other rule.', 1;

                DECLARE @Number int = @BaseVersionNumber + 1;
                DECLARE @VersionId uniqueidentifier;
                DECLARE @Inserted TABLE (Id uniqueidentifier NOT NULL);

                BEGIN TRANSACTION;

                -- Two publishes from the same base both compute @Number; the second fails on
                -- UX_PricingRuleSetVersions_VersionNumber (2601) and is rolled back.
                INSERT pricing.PricingRuleSetVersions (VersionNumber, ChangeTicketRef, ApprovedBy)
                OUTPUT inserted.Id INTO @Inserted
                VALUES (@Number, @ChangeTicketRef, @ApprovedBy);

                SELECT @VersionId = Id FROM @Inserted;

                INSERT pricing.PricingRules
                    (VersionId, Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
                SELECT @VersionId, Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier
                FROM @Rules;

                SET @NewVersionNumber = @Number;

                IF @DryRun = 1
                BEGIN
                    SELECT
                        COALESCE(n.Name, o.Name) AS Name,
                        o.Multiplier AS OldMultiplier,
                        n.Multiplier AS NewMultiplier,
                        CAST(CASE WHEN o.Multiplier IS NULL OR n.Multiplier IS NULL THEN NULL
                                  ELSE n.Multiplier / o.Multiplier END AS decimal(19,4)) AS Ratio,
                        CASE
                            WHEN o.Name IS NULL THEN N'Added'
                            WHEN n.Name IS NULL THEN N'Removed'
                            WHEN EXISTS (
                                SELECT n.Priority, n.PolicyType, n.MinCoverageLimitExclusive, n.MaxCoverageLimitInclusive, n.Multiplier
                                EXCEPT
                                SELECT o.Priority, o.PolicyType, o.MinCoverageLimitExclusive, o.MaxCoverageLimitInclusive, o.Multiplier)
                                THEN N'Changed'
                            ELSE N'Unchanged'
                        END AS Change
                    FROM @Rules n
                    FULL OUTER JOIN (
                        SELECT r.Name, r.Priority, r.PolicyType, r.MinCoverageLimitExclusive, r.MaxCoverageLimitInclusive, r.Multiplier
                        FROM pricing.PricingRules r
                        JOIN pricing.PricingRuleSetVersions v ON v.Id = r.VersionId
                        WHERE v.VersionNumber = @BaseVersionNumber
                    ) o ON o.Name = n.Name
                    ORDER BY COALESCE(n.Priority, o.Priority), COALESCE(n.Name, o.Name);

                    ROLLBACK TRANSACTION;
                    RETURN;
                END;

                COMMIT TRANSACTION;
            END;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE pricing.PublishRuleSetVersion;");
            migrationBuilder.Sql("DROP TYPE pricing.PricingRuleInput;");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "Policies")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PoliciesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "PricingRules",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "PricingRuleSetVersions",
                schema: "pricing");
        }
    }
}
