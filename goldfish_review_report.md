# Goldfish Annotated Review Report

**Status:** resolved — 24 fixes applied (auto accept); 3 escalated items and 1 contested item left unchanged.

**Target:** [`docs/designs/policy-pricing-rules.md`](docs/designs/policy-pricing-rules.md) (full file, lines 1–1589)

**Findings source:** [`consolidated_review_report.md`](consolidated_review_report.md), items C-B1 to C-N4, which merge the reports from Stages 5–9.

**Author decisions applied:** these come from the same report.

1. Matching stays in SQL.
2. Policies keep their update operation, with added controls.
3. API keys keep five permissions.
4. Pricing changes are never tied to a time.

**Summary:**

| Category | Count | Items |
| --- | --- | --- |
| PROPOSE FIX | 24 | the rest |
| ESCALATE | 3 | 15, 16, 17 |
| CONTEST | 1 | 26 |
| DECLINE | 0 | none |

## Prioritized Findings & AI Suggestions

### [Issue 1] [`egm-inventive-catalyst`, `egm-zoom-out`, `egm-implementation-readiness`] - Severity: Blocker

* **Affected Scope:** Lines 147–172 (CUJ 3, CUJ 5); 256; 298–307 (Choosing the active version); 354–375 (Publishing a version); 1143–1149 (file 34); 1358–1368 (file 55); 1472–1497 (file 66)
* **Discrepancy Description:** Publishing a full copy can silently undo another approved change. Suppose two scripts are prepared from version 1. The second publish overwrites the first one's change, and every check still passes. SERIALIZABLE combined with a MAX+1 version number is also likely to deadlock under concurrent publishes.
* **Rationale:** This breaks G2 and the audit trail, and the version history makes the revert look intentional. (C-B1)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The finding is valid, and the fix reuses the existing unique index on `VersionNumber`.
* ***Proposed Edit:***
  * **Location:** Publishing a version (parameters and steps)
    * Add the required parameter `@BaseVersionNumber int`.
    * Replace step 3 with: "Rejects the call with error 50003 unless `@BaseVersionNumber` equals the current highest version number, or is 0 when no version exists. The new version number is `@BaseVersionNumber + 1`. If two publishes from the same base run at once, the second violates the unique index on `VersionNumber` and is rolled back."
    * Delete "Inside one `SERIALIZABLE` transaction" and "The `SERIALIZABLE` lock prevents…". Replace them with "Inside one transaction (`SET XACT_ABORT ON`)".
  * **Location:** CUJ 3 and CUJ 5. Add "with `@BaseVersionNumber` = 1" (CUJ 3) and "= 2" (CUJ 5).
  * **Location:** Add CUJ 7, "A stale script is rejected". Given that version 2 exists, when a script built on base 1 is run, then it fails with error 50003 and nothing is inserted.
  * **Location:** File 34. Remove the SERIALIZABLE wording.
  * **Location:** File 55. The template sets `@BaseVersionNumber` and carries a comment telling the reviewer to check it against the active-version endpoint.
  * **Location:** File 66. Add two tests: a stale base is rejected with 50003, and two concurrent publishes on the same base produce exactly one new version.

### [Issue 2] [`egm-implementation-readiness`, `egm-codebase-details`, `egm-style`, `egm-comprehension`] - Severity: Blocker

* **Affected Scope:** Lines 156–162 (CUJ 4); 354–375; 1143–1149 (file 34); 1388–1389 (file 57); 1472–1497 (file 66); 1576–1577
* **Discrepancy Description:** The procedure's contract is undefined. The following are missing:
  * the columns of the table-valued parameter (TVP);
  * the parameter types;
  * how the new version number is returned;
  * whether blank values are rejected.

  The "THROW 50001–50005" codes map to steps that are not checks. Most rejections actually come from constraints, which raise error 547, 2601, or 2627.
* **Rationale:** Five files depend on this contract, so an implementer would end up designing it. (C-B2)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. The fix also covers how `@DryRun` returns its results (Issue 3).
* ***Proposed Edit:***
  * **Location:** Publishing a version. Add a "Procedure contract" subsection with three parts.
    * **TVP `pricing.PricingRuleInput`:**
      * `Priority int NOT NULL`
      * `Name nvarchar(200) NOT NULL`
      * `PolicyType nvarchar(32) COLLATE Latin1_General_BIN2 NULL`
      * `MinCoverageLimitExclusive decimal(18,2) NULL`
      * `MaxCoverageLimitInclusive decimal(18,2) NULL`
      * `Multiplier decimal(9,4) NOT NULL`
    * **Signature:**
      * `@ChangeTicketRef nvarchar(64)`
      * `@ApprovedBy nvarchar(128)`
      * `@BaseVersionNumber int`
      * `@DryRun bit = 0`
      * `@Rules pricing.PricingRuleInput READONLY`
      * `@NewVersionNumber int OUTPUT`
    * **Error table:**

      | Rejected condition | Where it is enforced | Error |
      | --- | --- | --- |
      | Blank ticket reference or approver | THROW | 50001 |
      | Empty rule set | THROW | 50002 |
      | Stale base version | THROW | 50003 |
      | Not exactly one catch-all rule | THROW | 50004 |
      | Catch-all does not have the largest priority number | THROW | 50005 |
      | Priority below 1 | `CK_PricingRules_Priority_Min` | 547 |
      | Multiplier not above 0 | `CK_PricingRules_Multiplier_Positive` | 547 |
      | Minimum not below maximum | `CK_PricingRules_CoverageRange` | 547 |
      | Unknown policy type | `CK_PricingRules_PolicyType` | 547 |
      | Duplicate priority | `UX_PricingRules_Version_Priority` | 2601 |
      | Duplicate name | `UX_PricingRules_Version_Name` | 2601 |
      | Duplicate ticket reference | `UX_PricingRuleSetVersions_ChangeTicketRef` | 2601 |
  * **Location:** Steps 1–5. Rewrite them to match the error table.
  * **Location:** CUJ 4. Change the outcome to "raises the error listed for that condition in the error table".
  * **Location:** File 34. Point to the error table, and replace "THROW 50001–50005 per step".
  * **Location:** File 57. Point to the error table.
  * **Location:** File 66. Each case asserts the error number from the table.
  * **Location:** Line 1576. Change the coverage requirement to "every row of the error table has a test".

### [Issue 3] [`egm-zoom-out`, `egm-inventive-catalyst`, `egm-design`, `egm-structural`] - Severity: Major

* **Affected Scope:** Lines 44–48; 196; 473–475; 649–659; 782–785; 1358–1368
* **Discrepancy Description:** Nothing checks the headline mistake (15 instead of 1.5) before it goes live. Dry run is neither in scope nor listed as a non-goal. (C-M1)
* **Rationale:** The most likely real error goes straight to customers.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. `@DryRun` is the smallest control that addresses it.
* ***Proposed Edit:***
  * **Location:** Publishing a version. Add a step:
    > "When `@DryRun = 1`, the procedure runs every check and then returns one row per rule: Name, old Multiplier, new Multiplier, Ratio, and Change (`Added`, `Removed`, `Changed`, or `Unchanged`). Rules are matched to the base version by `Name`, which is unique per version. It then rolls back. The ticket reviewer attaches this output to the ticket."
  * **Location:** Data model, PricingRules. Add the unique index (VersionId, Name).
  * **Location:** Goals. Add to G6: "A dry run shows each multiplier change against the live version before publishing."
  * **Location:** Accepted risks. Reword the "Large typos pass" mitigation to: "the dry-run diff in the ticket shows the ratio, and the review catches it".
  * **Location:** Future Improvements. Remove "diff report". Change "alert on multiplier jumps" to "automatic ratio threshold".
  * **Location:** File 55. The template runs the procedure with `@DryRun = 1` first.
  * **Location:** File 66. Add a dry-run test: the diff rows are correct and no version is inserted.

### [Issue 4] [`egm-zoom-out`, `egm-implementation-readiness`, `egm-codebase-details`, `egm-style`, `egm-design`] - Severity: Major

* **Affected Scope:** Lines 194; 309–352; 627–634; 865–880; 1177–1191; 1074; 1130
* **Discrepancy Description:** Matching stays in SQL (author decision 1). The fixes this needs are:
  * The `MatchRow` mapping is broken: the flag is an int, the column name doesn't match, and nulls are not handled.
  * `SqlQuery` and `SqlQueryRaw` are both used for the same thing.
  * The collation claim is wrong, because a case-insensitive CHECK accepts `commercial`.
  * Alternative M has no trade-off sentence.

  (C-M2)
* **Rationale:** As written, every quote would fail at runtime.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid, within the decision to keep SQL.
* ***Proposed Edit:***
  * **Location:** The matching SQL. Change the IsCatchAll expression to `CAST(CASE … END AS bit)`.
  * **Location:** Line 312. Change `SqlQueryRaw` to `SqlQuery` (FormattableString, with values passed as interpolation holes).
  * **Location:** Lines 347–348. Replace the sentence with:
    > "Both `PolicyType` columns use the binary collation `Latin1_General_BIN2`, so the CHECK rejects `commercial`, and the SQL comparison is case-sensitive like C#'s."
  * **Location:** Data model. Add `COLLATE Latin1_General_BIN2` to `PolicyType` in Policies, PricingRules, and Quotes.
  * **Location:** File 38. Declare `private sealed record MatchRow(Guid VersionId, int VersionNumber, Guid? RuleId, string? Name, int? Priority, decimal? Multiplier, bool? IsCatchAll)`. Map `Name` to `RuleName`.
  * **Location:** Files 28 and 33. Add `.UseCollation("Latin1_General_BIN2")` on PolicyType.
  * **Location:** Alternative M. Add to the reason:
    > "Matching in SQL keeps the precedence logic next to the constraints it relies on. The cost is that edge cases are tested only against LocalDB, so CI must run on Windows."

### [Issue 5] [`egm-zoom-out`, `egm-structural`, `egm-codebase-details`] - Severity: Major

* **Affected Scope:** Lines 174–181; 240–249; 389; 478–482; 683–693; 967–976; 1066–1080; 1164–1167; 1452–1455; 1525–1536
* **Discrepancy Description:** Policies keep their update operation (author decision 2). The fixes this needs are:
  * Nothing records who changed a policy.
  * Concurrent edits overwrite each other (last write wins).
  * The temporal configuration syntax is wrong.
  * The update's return type is undefined.

  (C-M3)
* **Rationale:** A base-price change can't be traced to whoever made it.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. The fix applies the controls the author chose.
* ***Proposed Edit:***
  * **Location:** Data model, Policies. Add two columns:
    * `ModifiedBy nvarchar(64) NOT NULL`, set to the caller's key name on create and update.
    * `RowVersion rowversion`.
  * **Location:** API table, `PUT`.
    * The request requires an `If-Match` header carrying the ETag (base64 RowVersion) returned by GET and POST.
    * It returns 200 with PolicyResponse and a new ETag.
    * It returns 412 `Policy.Concurrency` when the RowVersion doesn't match.
    * It returns 428 when `If-Match` is missing.
  * **Location:** CUJ 6.
    * Add "with `If-Match` of the current ETag".
    * Add "History keeps the earlier row, with its ModifiedBy".
    * Add a second Then: "A PUT with an old ETag returns 412."
  * **Location:** Accepted risks.
    * Delete "Last write wins".
    * Reword "Unreviewed base-price edits" to add "each edit records ModifiedBy in the history".
  * **Location:** File 4. Add `ModifiedBy` to `Create` and `Update`.
  * **Location:** File 5. Add `Concurrency` (Validation-type, mapped to 412 by a new `ErrorType.Conflict`).
  * **Location:** File 14. Add `ETag` to PolicyResponse.
  * **Location:** File 17. The command carries `ExpectedRowVersion` and `ModifiedBy`. The handler sets the original RowVersion value and maps `DbUpdateConcurrencyException` to `Policy.Concurrency`. It returns `Result<PolicyResponse>`.
  * **Location:** File 28.
    * Use `ToTable("Policies", tb => { tb.IsTemporal(t => { … }); tb.HasCheckConstraint(…); })`.
    * Add `Property(p => p.RowVersion).IsRowVersion()`.
  * **Location:** File 44. Map `ErrorType.Conflict` to 412.
  * **Location:** File 46. Read `If-Match`, and set the `ETag` response header.
  * **Location:** File 69. Add tests for a stale ETag returning 412, a missing If-Match returning 428, and ModifiedBy appearing in history.

### [Issue 6] [`egm-zoom-out`, `egm-inventive-catalyst`, `egm-design`, `egm-implementation-readiness`] - Severity: Major

* **Affected Scope:** Lines 377–379; 743–744; 756–758; 1150–1155
* **Discrepancy Description:** `READ_COMMITTED_SNAPSHOT` changes the whole database, needs ALTER DATABASE, ends connections, and cannot be undone by `Down`. Its stated reason is also wrong. (C-M4)
* **Rationale:** Rows are only ever inserted, and each publish is one transaction. So the default READ COMMITTED isolation already prevents a quote from reading half a version.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. Removing the setting also removes its permission and rollback problems.
* ***Proposed Edit:***
  * **Location:** Lines 377–379. Replace them with:
    > "Rules are only ever inserted, and each version is inserted in one transaction, so under SQL Server's default READ COMMITTED isolation a quote reads either the whole previous version or the whole new one. At worst a quote waits a few milliseconds for a publish to commit."
  * **Location:** NFR. Delete the RCSI performance bullet, the RCSI rollout bullet (`WITH ROLLBACK IMMEDIATE`), and the RCSI part of rollout step 1.
  * **Location:** File 34. Delete sub-step 3, the `ALTER DATABASE`, and the related `Down` note.

### [Issue 7] [`egm-zoom-out`, `egm-design`, `egm-codebase-details`, `egm-implementation-readiness`] - Severity: Major

* **Affected Scope:** Lines 85–86; 434–450; 661–668; 734–736; 1211–1245; 1275–1277; 1306; 1313–1316
* **Discrepancy Description:** API keys keep five permissions (author decision 3). The fixes this needs are:
  * There is no fallback policy, so a new endpoint is open until someone adds `[Authorize]` to it.
  * Several handler details are left undefined: name length, claim type, authentication type, how options bind, hex comparison, and the options source.
  * Key strength, HTTPS, and rotation are not stated.
  * Alternative P weighs only setup cost.

  (C-M5)
* **Rationale:** Several of these fail silently and treat callers as anonymous.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid, within the decision to keep API keys.
* ***Proposed Edit:***
  * **Location:** NFR Security. Replace "No global authorization policy…" with:
    > "A fallback authorization policy requires an authenticated caller on every endpoint. `ProductsController` is marked `[AllowAnonymous]`."
  * **Location:** API key authentication. Add:
    > "Keys are 32 random bytes, encoded as base64url. Keys are sent only over HTTPS. Configuration is read at startup, so rotating a key needs a restart."
  * **Location:** File 41.
    * Use properties `{ get; set; }`.
    * Initialize `Keys = new()`.
    * Validate that `Name` is 1–64 characters (matching `RequestedBy`/`ModifiedBy`).
  * **Location:** File 43.
    * The constructor takes `IOptionsMonitor<ApiKeyOptions>`.
    * Hashes are compared with `Convert.FromHexString(entry.KeyHash)` and `CryptographicOperations.FixedTimeEquals`.
    * Create the principal as `new ClaimsIdentity(claims, "ApiKey")` with `ClaimTypes.Name`.
  * **Location:** File 46. Read the caller as `User.Identity?.Name ?? throw new InvalidOperationException()`.
  * **Location:** File 49. Add `.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`.
  * **Location:** File 45. Add `[AllowAnonymous]`.
  * **Location:** File 52. Use `https://localhost:7258`.
  * **Location:** Alternative P. Add:
    > "API keys were kept because no identity provider has been confirmed for these callers. The accepted cost is that keys identify calling systems, not people, and rotation is manual."
  * **Location:** Add a new Alternative V, "Two roles instead of five permissions". Pros: less configuration. Cons: less control over what each key can do. Rejected by author: separate permissions let a quoting system be denied policy writes.

### [Issue 8] [`egm-zoom-out`, `egm-design`, `egm-implementation-readiness`, `egm-codebase-details`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 452–467; 473–475; 779; 931–937; 991–1015; 1047–1055
* **Discrepancy Description:** The quote metric has no exporter, and `AddMetrics()` won't compile in the Application project. Catch-all quotes logged at Warning would become noise. The handler's error log can never be reached. (C-M6)
* **Rationale:** This adds code that no one can see, and the build breaks.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. The data already stored covers the monitoring need.
* ***Proposed Edit:***
  * **Location:** Logging and visibility. Replace the Warning sentence and the metric sentence with:
    > "Quotes priced by the catch-all are logged at Information with `WasCatchAll = true`. Operators monitor the catch-all rate and per-version volume with a query over `pricing.Quotes` grouped by VersionNumber and WasCatchAll, documented in the runbook."
  * **Location:** Delete file 20 (`QuoteMetrics.cs`). Remove `Diagnostics.Abstractions` from file 13. Remove `AddSingleton<QuoteMetrics>()` and `AddMetrics()` from file 26.
  * **Location:** File 21. Split the steps into two paths.
    * **Success path:** load, match, create, save, then log Information with the template `"Quote {QuoteId} for policy {PolicyId} priced by version {VersionNumber} rule {RuleId} (catch-all {WasCatchAll}) multiplier {Multiplier} final {FinalPrice} for {RequestedBy}"`.
    * **Failure path:** before returning, log by error.
      * `Policy.NotFound`: Information.
      * `Pricing.NoActiveVersion`: Warning.
      * `Pricing.InvalidActiveVersion`: Error.

      Each log includes PolicyId and RequestedBy.
  * **Location:** Alternative I and Accepted risks. Replace "logged at Warning" and "metric" with "flagged by `WasCatchAll` and the runbook query".
  * **Location:** Future Improvements. Add "Metrics exported through OpenTelemetry".

### [Issue 9] [`egm-design`, `egm-codebase-details`, `egm-implementation-readiness`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 75–77; 356–363; 381–395; 1335–1356; 1545–1558
* **Discrepancy Description:** The publisher can't declare the TVP without a grant on the type. The API login has no rights on `dbo.Products`, which breaks today's endpoints. G5 overstates its guarantee. The migrator's rights are not listed as a risk, and the schema owner is not stated. (C-M7)
* **Rationale:** The first publish fails, and the existing endpoints break.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** G5. Replace with:
    > "**G5. Rule history cannot be rewritten through normal access.** No login used by the API or by ticket scripts can update or delete published rules or quotes, and SQL Server enforces this with permissions. Each version records the change ticket that approved it. Logins with db_owner, sysadmin, or schema-change rights are an accepted risk."
  * **Location:** Permissions table.
    * API row: add "SELECT, INSERT on `dbo.Products`".
    * Publisher row: add "EXECUTE on TYPE::`pricing.PricingRuleInput`".
    * Add the sentence: "The `pricing` schema and all its objects are owned by `dbo`, which ownership chaining requires."
  * **Location:** Accepted risks. Add:
    > "The migrator login can alter tables or the procedure. Migrations are code-reviewed and run only during deployments."
  * **Location:** Files 53, 54, and 71. Add both grants. Test 71 asserts that the publisher can declare the type and that the API user can read Products.

### [Issue 10] [`egm-implementation-readiness`, `egm-codebase-details`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 1335–1346; 1545–1559
* **Discrepancy Description:** The grant script's sqlcmd variables, its batches, and the claim that it can be re-run safely don't work with an ADO.NET test. (C-M8)
* **Rationale:** The implementer would have to write a sqlcmd runner.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** File 53.
    * "Variables `$(ApiUser)` and `$(PublisherUser)` are database user names."
    * "Each user is created `FOR LOGIN` only when a login with that name exists and the user does not, checked in `sys.database_principals`."
    * "The script contains no `GO` separators, so it runs as a single batch."
  * **Location:** File 71. Change to:
    > "The test reads the script and replaces `$(ApiUser)`/`$(PublisherUser)` with `api_test`/`publisher_test`. It runs the script twice through `SqlCommand` and asserts the second run succeeds, then runs the checks."
  * **Location:** Line 1558. Delete "The grant script is tested, not just written."

### [Issue 11] [`egm-implementation-readiness`, `egm-codebase-details`, `egm-structural`] - Severity: Major

* **Affected Scope:** Lines 632–634; 1409–1478; 1499–1510; brief in-scope goals
* **Discrepancy Description:** The test setup is incomplete:
  * the test connection-string override could fall back to the developer database;
  * package versions are not pinned;
  * the fixture, collection, and reset contract is undefined;
  * there is no access to internal types;
  * test keys are not listed;
  * testing is not part of the scope.

  (C-M9)
* **Rationale:** The tests could delete data in the developer's database.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** NFR. Add a Testing item:
    > "Domain unit tests cover policy validation and price rounding. Integration tests against LocalDB cover matching, the procedure's error table, the endpoints, and permissions. CI therefore runs on Windows (decision 13)."
  * **Location:** Decisions table. Add row 13: "Test database: LocalDB, because matching runs in SQL (decision 6)."
  * **Location:** Files 59 and 62. Pin these packages:
    * `xunit.v3` 3.x (exact version at implementation time)
    * `xunit.runner.visualstudio`
    * `Microsoft.NET.Test.Sdk`
    * `coverlet.collector`
    * `Microsoft.AspNetCore.Mvc.Testing` 10.0.15

    Also add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
  * **Location:** File 63.
    * All integration tests share one `[Collection("Database")]`, which runs them one at a time.
    * `ResetAsync` runs before each test and deletes rows in this order: Quotes, PricingRules, PricingRuleSetVersions, Policies.
    * It then turns `SYSTEM_VERSIONING` off, clears `PoliciesHistory`, and turns versioning back on.
  * **Location:** File 64.
    * Override with `builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString)`.
    * After the host builds, assert that the resolved database name starts with `Goldfish_Test_`.
    * Tests resolve services from `factory.Services.CreateScope()`.
    * Each of the keys `full`, `quoter`, and `reader` is a fixed 32-byte test value. Its SHA-256 hash is computed in the factory, not stored. `reader` has `policies:read`, `quotes:read`, and `pricing:read`.
  * **Location:** Add file 72, `src/Goldfish.Infrastructure/Goldfish.Infrastructure.csproj` (Modify): add `InternalsVisibleTo Goldfish.IntegrationTests`.

### [Issue 12] [`egm-implementation-readiness`, `egm-codebase-details`] - Severity: Major

* **Affected Scope:** Lines 791–1559
* **Discrepancy Description:** The plan has no phases or checkpoints. (C-M10)
* **Rationale:** Some changes break the build until later files land.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** The start of the Detailed Implementation Plan. Add a "Phases" table. Each phase ends with `dotnet build` and the test command listed.

    | Phase | Contents | Files | Exit check |
    | --- | --- | --- | --- |
    | 1 | Error types | 1, 2, 44, 45 | Build |
    | 2 | Policies | 3–6, 14–18, 27, 28, 36, 46 (policy actions), 59, 60, and the policy part of the migration | Domain tests |
    | 3 | Pricing schema and procedure | 9, 30–35, 63, 65, 66 | Procedure tests |
    | 4 | Matching and quotes | 7, 8, 10–12, 19, 21–26, 29, 37–40, 46 (quote action), 47, 48, 61, 67, 68, 70 | All tests |
    | 5 | Authentication | 41–43, 49–52, 64 | All tests |
    | 6 | Operations | 53–58, 62, 69, 71, 72 | All tests and a manual check |

    The migration is generated once, in phase 3, after files 27–33 exist. Phase 2 adds no migration.

### [Issue 13] [`egm-style`, `egm-implementation-readiness`, `egm-codebase-details`] - Severity: Major

* **Affected Scope:** Lines 1139–1140; 1563–1583
* **Discrepancy Description:** The verification gates can't be run as written:
  * no coverage tool;
  * `TreatWarningsAsErrors` is not added;
  * `ef database update` targets the developer database;
  * the CI pipeline doesn't exist;
  * the manual check has no step that creates a policy.

  (C-M11)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid. (The tool manifest part is contested in Issue 26.)
* ***Proposed Edit:***
  * **Location:** Migrations bullet. Use `dotnet ef database update --connection "Server=(localdb)\MSSQLLocalDB;Database=Goldfish_Verify;Trusted_Connection=True;TrustServerCertificate=True"`, using the dotnet-ef 10.0.15 pinned in `dotnet-tools.json` (`dotnet tool restore`).
  * **Location:** Coverage. Run `dotnet test --collect "XPlat Code Coverage"` (coverlet.collector). The 90% line coverage applies to `Policy` and `Quote` in `Goldfish.Domain`, and is checked manually from the report.
  * **Location:** Integration tests. Replace "CI agents must be Windows" with "Integration tests need Windows with LocalDB; any CI pipeline added later must use Windows agents."
  * **Location:** Manual. Add steps:
    * "`POST /api/policies` with the `full` dev key (Commercial, 2,000,000, 1,234.57)."
    * "Run `db/tickets/0001-initial-rule-set.sql` with sqlcmd as the developer's Windows login."
  * **Location:** Static analysis. Rename it "Compiler warnings".

### [Issue 14] [`egm-design`, `egm-style`, `egm-structural`, `egm-zoom-out`] - Severity: Major

* **Affected Scope:** Lines 53–55; 298–307; 483–485; 592–615
* **Discrepancy Description:** The no-delay requirement is worded inconsistently, and Alternative J has no real reason for rejection. (C-M12)
* **Rationale:** G2 and the decision not to cache both rest on this requirement.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The author has decided that no pricing change is tied to a time (author decision 4).
* ***Proposed Edit:***
  * **Location:** Lines 53–55. Replace with:
    > "**Stale rules are not acceptable.** Once a change is approved and its script commits, the next quote must use it. A delay of even a few seconds is unacceptable to the business. That rules out holding rules in memory and refreshing them on a timer."
  * **Location:** Line 614 (Alternative K). Keep "unacceptable".
  * **Location:** Lines 305–307. Replace with: "No pricing change is tied to a date or time. A change applies when its approved script runs."
  * **Location:** Accepted risks. Delete "Manual timing for scheduled changes".
  * **Location:** Alternative J. Replace the reason with:
    > "Rejected by author: no pricing change is tied to a date or time, so effective dates would add a column, a clock comparison, and a scheduled-versions view for no current need. If one appears, an approved script can be run as a scheduled job without schema changes."
  * **Location:** Decisions table, row 4. Add "(no pricing change is tied to a time)".

### [Issue 15] [`egm-comprehension`, `egm-zoom-out`] - Severity: Major

* **Affected Scope:** Lines 12–15; 303–307; 390; 753; 1358–1368
* **Discrepancy Description:** The document never says who writes, approves, and runs ticket scripts, or with which login and tool. (C-M15)
* **Rationale:** The main workflow can't be followed from start to finish.
* ***AI Suggestion:*** ESCALATE
* ***Justification:*** The finding is valid, but the facts are outside every file the reviewers could read. Nothing in the conversation or the brief says who in your organization runs approved scripts, or how. Guessing would put an invented process into the design.
* ***Proposed Edit:***
  * **Location:** After "Publishing a version", add a "Publishing workflow" paragraph built from your answers to three questions:
    1. Who drafts the script? (An analyst? A developer?)
    2. Who runs it after approval? (A DBA? A pipeline?)
    3. Which tool runs it, and how does that person or pipeline get the `goldfish_pricing_publisher` credentials?

### [Issue 16] [`egm-structural`, `egm-design`] - Severity: Major

* **Affected Scope:** Lines 670–681 (Alternative Q)
* **Discrepancy Description:** Ledger tables would close the db_owner risk, but the spec never says why the weaker permission-based option is enough. (C-M13, part)
* **Rationale:** The alternative is rejected without a weighed trade-off.
* ***AI Suggestion:*** ESCALATE
* ***Justification:*** The deciding fact is outside every file: which SQL Server version and edition production runs. Ledger tables need SQL Server 2022+ or Azure SQL. If production meets that, the rejection reason changes.
* ***Proposed Edit:***
  * **Location:** Alternative Q. Add a trade-off sentence based on your answer.
    * **If production is older than SQL Server 2022:** "Ledger tables are not available on the production SQL Server version."
    * **If production is 2022 or newer:** "Permissions were chosen because ledger tables can never be purged, which would block a future retention policy."

### [Issue 17] [`egm-design`, `egm-style`] - Severity: Nit (raised to the user because it needs a fact)

* **Affected Scope:** Lines 195; 645–647
* **Discrepancy Description:** Nobody has confirmed banker's rounding with finance. (Stage 7 N14)
* ***AI Suggestion:*** ESCALATE
* ***Justification:*** The author chose the rounding mode in this session. Whether finance agreed is not recorded anywhere a reviewer could read.
* ***Proposed Edit:***
  * **Location:** Open Questions. Add "**Rounding sign-off**: has finance confirmed banker's rounding (ToEven) for quoted prices?" Or record who confirmed it, if someone has.

### [Issue 18] [`egm-structural`, `egm-design`, `egm-style`, `egm-comprehension`] - Severity: Major

* **Affected Scope:** Lines 509–720 (Alternatives); 769–775
* **Discrepancy Description:** The link to the brief is broken, and the URLs are not written as links. Several "Rejected by author" entries give no trade-off. Alternative T bundles three options. The alternative "most specific match wins" is missing. "Rejected in review" is never explained. (C-M13, except Q)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** Additional Resources.
    * `[Concept brief](../../concepts_brief.md)`
    * `[EF Core temporal tables](https://learn.microsoft.com/ef/core/providers/sql-server/temporal-tables)`
    * `[Ownership chains](https://learn.microsoft.com/sql/relational-databases/security/ownership-chains)`
  * **Location:** The top of Alternatives. Add:
    > "'Rejected in review' means rejected by the multi-reviewer analysis recorded in the [concept brief](../../concepts_brief.md)."
  * **Location:** Alternatives G, H, and L. Add one sentence each:
    * **G:** "Full copies keep each quote reproducible from one version id."
    * **H:** "Lowest-wins matches how analysts already say 'priority 1'."
    * **L:** "Two condition fields don't justify a generic condition model."
  * **Location:** Alternative T. Split it into T1 (fixed base), T2 (base in the request), and T3 (external system), each with its own reason.
  * **Location:** Add Alternative W, "Winner chosen by most-specific match, highest result, or newest rule".
    * Pros: no priority numbers to maintain.
    * Cons: "most specific" has no single definition, so ties need a hidden rule; highest-result raises prices whenever rules overlap; newest-rule depends on insert order.
    * Reason: Rejected by author in favor of an explicit priority number.

### [Issue 19] [`egm-structural`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 305–307; 394–395; 471–485; 754–765
* **Discrepancy Description:** Risks are spread across four places, and some have no mitigation. (C-M14)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** Move "Accepted risks" out of the Technical Plan into a new H2 section, "Risks & Mitigations", placed after Non-Functional Requirements.
  * **Location:** Format. Each risk gets a type (correctness, security, operations), a mitigation, and an "Accepted because…" line.
  * **Location:** The risks are:
    * large typos (mitigated by the dry-run diff);
    * privileged logins;
    * the migrator login;
    * unreviewed base-price edits (mitigated by ModifiedBy and the history table);
    * running the `Down` migration in production.
  * **Location:** Permissions and Rollback. Replace the inline copies with "see Risks & Mitigations".

### [Issue 20] [`egm-codebase-details`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 1102–1133
* **Discrepancy Description:** Nothing generates `PricingRules.Id`, so the procedure's INSERT fails. (C-M16)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** Data model, PricingRules Id. Set the rule to "Primary key, default `NEWSEQUENTIALID()`".
  * **Location:** File 33. Add `Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()")`.

### [Issue 21] [`egm-design`, `egm-zoom-out`] - Severity: Major

* **Affected Scope:** Lines 114–120; 280–296; 1090
* **Discrepancy Description:** The data-model table leaves out the `PolicyId` index that file 29 already plans, and there is no storage estimate. (C-M17)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Partly valid. The index already exists in file 29; only the table leaves it out.
* ***Proposed Edit:***
  * **Location:** Under the Quotes table. Add:
    > "Indexes: `PolicyId`. At the assumed tens of quotes per second, Quotes grows by roughly 30–300 million rows a year (about 10–90 GB before indexes). Archiving is deferred until retention is decided."

### [Issue 22] [`egm-style`, `egm-comprehension`, `egm-design`] - Severity: Major

* **Affected Scope:** Lines 22–23; 245; 271; 342–348; 369; 877–878; 1038
* **Discrepancy Description:** Some wording could be implemented wrongly:
  * the priority direction;
  * "has started";
  * the hedge on the no-match case;
  * the collation claim (covered in Issue 4).

  (C-M18)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** Line 22. Change to "the matching rule with the lowest priority number sets the price".
  * **Location:** Line 369. Change to "that rule's priority number is larger than every other rule's".
  * **Location:** Lines 342–344. Replace "which the catch-all requirement should make impossible" with "which can happen only if rule rows were written outside `pricing.PublishRuleSetVersion`, for example by a privileged login".
  * **Location:** Lines 877–878 and 1038. Change "when no version has started" to "when no version has been published".

### [Issue 23] [`egm-implementation-readiness`, `egm-codebase-details`, `egm-style`] - Severity: Major

* **Affected Scope:** Lines 793–796; 936; 1439–1440
* **Discrepancy Description:** "Pinned exactly" contradicts "10.0.x", and the `SqlClient` reference is left undecided. (C-M19)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Valid.
* ***Proposed Edit:***
  * **Location:** Line 793. Change to:
    > "Microsoft packages are pinned at 10.0.15, matching the existing EF Core references. Test-only packages are pinned to their latest stable versions at implementation time."
  * **Location:** File 13. Pin `Microsoft.Extensions.Logging.Abstractions` at 10.0.15.
  * **Location:** File 62. Add "`Microsoft.Data.SqlClient` comes through EF Core SqlServer; no separate reference."

### [Issue 24] [`egm-structural`, `egm-style`] - Severity: Nit

* **Affected Scope:** Lines 7; 195–200; 249; 291; 401–406; 725; 767–789
* **Discrepancy Description:** Structure. (C-N1)
  * Additional Resources and Future Improvements are H3 headings.
  * Horizontal rules are used inconsistently.
  * Decision-table rows don't match the column header.
  * Some data-model rows cover several columns.
  * API error cells are uneven.
  * There are no CUJs for "no key" or "no version yet".
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Cheap, and it helps readers.
* ***Proposed Edit:***
  * Promote both sections to H2, and use a horizontal rule before every H2.
  * Rename the decisions column to "Topic".
  * Split "PeriodStart, PeriodEnd" into separate rows, and give the Quotes snapshot columns their own rows with types.
  * Write every API error cell as "status `Code`". Add 401 and 403 to each protected row.
  * Add two short CUJs:
    * CUJ 8: no key returns 401, and nothing is saved.
    * CUJ 9: a quote before the first publish returns 503 `Pricing.NoActiveVersion`.

### [Issue 25] [`egm-design`, `egm-codebase-details`, `egm-implementation-readiness`, `egm-style`, `egm-zoom-out`] - Severity: Nit

* **Affected Scope:** Throughout
* **Discrepancy Description:** Design accuracy, code conventions, and wording. (C-N2, C-N3, C-N4)
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Each fix is a small text change, and together they remove ambiguity an implementer would otherwise have to resolve.
* ***Proposed Edit:***
  * **Location:** Data model, Quotes.
    * Add a composite foreign key on (VersionId, RuleId). This needs a unique index on PricingRules (VersionId, Id).
    * Add a CHECK on `PolicyType`.
    * Add the note "CreatedOnUtc and PublishedOnUtc are for the record only and come from different clocks; they are never compared."
  * **Location:** G4. Change to "Explaining a past quote needs only the quote row and the immutable rule rows it references."
  * **Location:** The decimal rule in file 4. Use "`decimal.Round(value, 2) != value` is rejected". Add "values above 9,999,999,999,999,999.99 are rejected as `Policy.InvalidCoverageLimit` or `InvalidBasePrice`".
  * **Location:** Files 6, 8, 12, and 24. Use `CancellationToken cancellationToken = default`.
  * **Location:** File 36. `GetByIdAsync` uses `AsNoTracking`. Add `GetForUpdateAsync`, which tracks the entity.
  * **Location:** File 49. Remove `public partial class Program;`, which .NET 10 generates (ASP0027).
  * **Location:** Files 28, 29, and 33. Name every CHECK constraint with the `CK_<Table>_<Rule>` pattern. Copy the UTC converter lambda inline. Add `ValueGeneratedNever` on Quotes.Id.
  * **Location:** Files 30 and 31. Records use `string?` for PolicyType. Navigation: `PricingRuleSetVersionRecord.Rules`. Foreign key: `FK_PricingRules_Versions`.
  * **Location:** File 55. Literal placeholders: `'CHG-0000'`, `'name@example'`.
  * **Location:** File 57. Add the key one-liner: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`.
  * **Location:** File 63. Change "the test user is sysadmin" to "the developer's Windows login, which is sysadmin on LocalDB".
  * **Location:** The Overview. Add: "Callers are internal systems, for example an underwriting portal, each with its own API key."
  * **Location:** Throughout. Write "authorization policy" for the ASP.NET sense. Use `ApiKeys:Keys` in the Technical Plan. Use `WasCatchAll` in the SQL alias.
  * **Location:** Wording.
    * Line 39: "adds a release cycle to each change".
    * Line 47: "values outside the limits listed in G6".
    * Line 57: "Goldfish has no pricing support today."
    * Lines 80 and 374: "a minimum not below the maximum".
    * Line 84: "with the fields listed under Logging".
    * Line 755: "no quote is priced or saved, and the error code names the cause".
    * Delete "mature", "deliberately", "a safe default", "fast", "very", and "always" (lines 517, 1155, 1315, 1413, 1508–1509).
    * Line 1308: "Enables authentication (G8)".
  * **Location:** Technical Plan matching SQL. Keep the SQL, because the author kept SQL matching and the query is the contract. Trim the code-level calls in the API key section to the facts listed in Issue 7.
  * **Location:** Quotes copies of VersionNumber, RuleName, and Multiplier. Keep them, and add the note "copied deliberately so a quote stays readable even if a privileged login alters rule rows (see Risks)".

### [Issue 26] [`egm-implementation-readiness`] - Severity: Major (part of Stage 9 M13)

* **Affected Scope:** Lines 1563–1583
* **Discrepancy Description:** "There is no `.config/dotnet-tools.json` for dotnet-ef 10.0.15."
* ***AI Suggestion:*** CONTEST
* ***Justification:*** The file [`dotnet-tools.json`](dotnet-tools.json) exists at the repository root and pins `dotnet-ef` 10.0.15. The reviewer could have read it. The plan only needs to mention it, which Issue 13 does.
* ***Proposed Edit:*** No separate change. Covered by Issue 13.
