# Brainstorm Findings: Pricing Rules Admin Web App (Blazor, in-app approval and publish)

**Mode:** BRAINSTORM
**Concept:** An internal .NET 10 server-rendered Blazor app in the Goldfish solution. Analysts sign in with Entra ID and build rule-set drafts in forms. A Manager who is not the drafter (checked by Entra object ID) approves, and that approval publishes immediately through `pricing.PublishRuleSetVersion`. The app holds the publisher login, keeps an append-only audit trail, and writes its change-request ID to `ChangeTicketRef`.

## Reviewer Status

| Reviewer | Role title | Status |
| --- | --- | --- |
| `egm-design` | Skeptical Architect Brainstormer | completed |
| `egm-codebase-details` | Codebase Accuracy & Reuse Brainstormer | completed |
| `egm-zoom-out` | Simplicity & Scope Brainstormer | completed |
| `egm-comprehension` | Naive Reader Comprehension Brainstormer | completed |
| `egm-implementation-readiness` | Implementation Readiness Brainstormer | completed |
| `egm-structural` | Structural Brainstormer | completed |
| `egm-inventive-catalyst` | Inventive Catalyst Brainstormer | completed |

**Baseline path correction.** The migration path in the brief (`src\Goldfish.Infrastructure\Migrations\...`) does not exist. The real file is `C:\Goldfish\src\Goldfish.Infrastructure\Persistence\Migrations\20261121155539_AddPolicyPricing.cs`, and every reviewer used that file. All "migration" line numbers below refer to it. Several reviewers read only lines 1-1056 of the 1,959-line design doc. Lines 1057 onward are the per-file implementation plan.

**Abbreviations.** In the source references, "design" means `docs/designs/policy-pricing-rules.md` and "runbook" means `docs/pricing-rules.md`.

**Severity totals after deduplication:** 8 Blockers, 15 Majors, 9 Nits.

## Headline

The concept bundles two changes.

1. **Forms instead of hand-written SQL.** This fixes the stated pain.
2. **Approval and publishing moved into the app, with the app holding the publisher login.** This is a governance and security change that the stated pain does not require. It also reverses a documented Non-Goal and a rejected alternative.

Every lens found the same core gaps:

- Four-eyes review would be enforced only in C#.
- Publishing and auditing are two separate writes.
- Lifecycle and rebase rules are undefined.
- The success measure targets a failure the system already prevents, and ignores the real risk: a valid but wrong value such as 15 instead of 1.5.

---

## Blockers

### B1. The concept reverses the existing design without saying what it supersedes
- **Lenses:** all seven.
- **Scope:**
  - In design:
    - the Non-Goal at lines 109-111 ("A rule-editing screen or in-app approval workflow");
    - Alternative U at lines 858-864;
    - G5/G6 at lines 85-95;
    - the "only through change-ticket scripts" Non-Goal at lines 127-128;
    - CUJs 3, 4, 5 and 7 (lines 172-219);
    - Decision 10 and the permissions table at line 516;
    - the risk row at line 934;
    - Status: "Proposed" at line 4.
  - In the runbook: "Publishing a rule change" at lines 27-45.
- **Description:** A reader gets two authoritative processes that contradict each other. The external change ticket may also be an organisational or regulatory control, and nothing records whether compliance or change management accepts in-app approval in its place.
- **Recommendation:**
  - Add a "Relationship to existing design" section that marks each goal, non-goal, CUJ and decision as Kept, Changed or Withdrawn.
  - Amend the old design and the runbook accordingly.
  - Get sign-off from the owner of change management.
  - Give the new goals their own prefix (for example W1..Wn), so G1-G9 keep their meaning.

### B2. The scope is larger than the pain: syntax errors arise in authoring, not in review
- **Lenses:** zoom-out (Blocker), structural, inventive-catalyst.
- **Scope:** the concept's approval, publish and login scope.
- **Description:** A form that pre-fills the live rule set, validates it, and produces a ready-to-run script or dry-run output for the existing ticket removes syntax errors and the retyping chore. It does this without moving approval in-house, giving a web host the publisher credential, building a state machine, or needing an Entra role model. The design recorded no new reason to overturn Alternative U.
- **Recommendation:**
  - Phase 1 (authoring only): pre-fill from the live version, validate, show the diff, and export or attach to the ticket. The cheapest version is a small CLI or query that writes `db/templates/publish-rule-set-version.sql` pre-filled with the current rows and `@BaseVersionNumber`.
  - Phase 2 (in-app approval and publish): a separate concept, justified on its own grounds such as ticket turnaround time.
  - If both phases stay in one concept, Alternatives Considered must reject "form generates the script, ticket review kept" with a concrete trade-off.

### B3. Four-eyes review exists only in app code, and the publisher login trusts any approver string
- **Lenses:** design (Blocker), inventive-catalyst (Blocker), codebase-details, zoom-out, structural.
- **Scope:**
  - Migration lines 220-221: the procedure checks only that `@ApprovedBy` and `@ChangeTicketRef` are not blank.
  - `grant-permissions.sql` line 40: the publisher login has bare EXECUTE on the procedure.
- **Description:**
  - Anything that can act as the app can publish any rule set under any `ApprovedBy`, with no second person: an app compromise, a developer with production configuration, an authorization bug, or an Entra role misassignment.
  - The drafter is never stored with the published version.
  - The old ticket login also stays usable, so approval in the app is not exclusive.
- **Recommendation:**
  - Give the app its own login (for example `goldfish_rules_admin`) with no EXECUTE on the inner procedure.
  - Grant that login EXECUTE only on a new dbo-owned wrapper, for example `rulesadmin.ApproveAndPublish(@RevisionId, @ApproverOid, ...)`. Ownership chaining lets the wrapper call the inner procedure.
  - The wrapper:
    - reads the rules from the stored, immutable submitted revision (the app never passes rules on the approve path);
    - checks the revision is Submitted and undecided;
    - checks the approver is not any contributor;
    - checks the content hash;
    - checks the request has not already been published.
  - Authenticate the app to SQL Server with a gMSA or managed identity, not a SQL password.
  - Keep `goldfish_pricing_publisher` only as an alerted break-glass login, or retire it.
  - **Residual risk:** the approver's object ID still comes from the app. The wrapper makes every forgery leave a matching draft, submission and approval trail. True per-user SQL identity needs Entra authentication to SQL Server, which this environment may not support (unconfirmed).

### B4. Publish and audit are a dual write, and retries can turn a successful publish into a "failure"
- **Lenses:** all except zoom-out. Implementation-readiness, inventive-catalyst and codebase-details rated it Blocker.
- **Scope:**
  - Migration lines 251/299: the procedure owns its transaction, with `SET XACT_ABORT ON` at line 218.
  - `DependencyInjection.cs:25` (`EnableRetryOnFailure`).
  - `CreateQuoteHandler.cs:35`.
  - The `UX_PricingRuleSetVersions_ChangeTicketRef` index at migration lines 162-167.
- **Description:** Three failure modes:
  - The version commits but the app's "Published" or audit write fails. A live price change then has no audit record.
  - The commit succeeds but its acknowledgement is lost. A retry (EF execution strategy, double-click, or manual) gets 50003 or 2601. The UI says "rebase", and the analyst may republish a change that is already live.
  - Wrapping the call in an app transaction breaks too. The dry-run `ROLLBACK TRANSACTION` at line 295 kills the outer transaction (error 266), and with XACT_ABORT any THROW dooms it.
  - Note: `EnableRetryOnFailure` retries only commands sent through EF. A raw ADO.NET call like `RuleSetPublisher` is not retried automatically, but a user or manual retry has the same effect.
- **Recommendation:**
  - Best option: write the approval event in the same transaction as the publish, inside the B3 wrapper. The inner COMMIT only lowers `@@TRANCOUNT`. Never call the procedure with `@DryRun = 1` inside that outer transaction.
  - Otherwise:
    - write PublishRequested, then EXEC (no retry strategy, no ambient transaction), then Published or PublishFailed;
    - treat the change-request ID in `ChangeTicketRef` as the idempotency key;
    - after any ambiguous error, 50003 or 2601, look up `WHERE ChangeTicketRef = @id` with a login that can read before recording an outcome;
    - run a reconciliation job that alerts when a version and a Published request do not pair up.

### B5. The draft lifecycle and the "approve failed" path are undefined
- **Lenses:** comprehension (Blocker), structural, implementation-readiness, design.
- **Scope:** the concept as a whole. The failure cases come from the error table at design lines 480-496.
- **Description:** The concept names actions but no states. Open questions:
  - What state is a draft in after a failed approval?
  - Is Rejected final?
  - Can a draft be withdrawn?
  - Can it be edited while Submitted?
  - Do drafts expire?
  - What happens when two managers click Approve at once?
- **Recommendation:**
  - Write a state table with these states: Draft, Submitted, Publishing, Published, Rejected, PublishFailed/NeedsRebase, Withdrawn.
  - For each transition, give who may trigger it (role, plus the not-a-contributor rule) and the audit event it writes.
  - Add one failure row per row of the procedure's error table.
  - Use a rowversion on the change request so a concurrent approval loses cleanly.
  - Make approval atomic: the request either becomes Published with version N, or stays Submitted with a visible reason.

### B6. Rebase is undefined and can silently undo another approved change
- **Lenses:** comprehension (Blocker), design, structural, implementation-readiness, inventive-catalyst. Zoom-out offered a simpler contrary option.
- **Scope:**
  - Decision 1 (full copies), design line 243.
  - The G2 wording "no publish can silently undo another approved change", design line 78.
  - The 50003 check at migration lines 226-233.
  - Name matching at migration line 292.
- **Description:** The stale-base check only proves that the draft names the latest version number, not that it contains the changes made since. If rebasing means "re-point the base", the draft overwrites the intervening publish. Any approval of the pre-rebase content no longer covers the rebased content.
- **Recommendation:**
  - Define rebase as a three-way merge keyed by rule `Name` (old base, new latest, draft), with conflicts resolved by hand.
  - A rebased draft always returns to Submitted and needs a fresh approval and a fresh diff. This holds even for changes that look disjoint, because priority ordering couples rules.
  - The change-request ID stays the same across rebases.
  - Zoom-out alternative, which fits about 12 publishes a year: no merge. On 50003, offer "start a new draft from the latest version", with the old values shown for re-entry.

### B7. The success measure is met trivially and ignores the real risk
- **Lenses:** comprehension (Blocker), zoom-out (Blocker), design, inventive-catalyst.
- **Scope:**
  - Design lines 49-54 and 934, decision 8 (line 250), and Alternative O.
  - Runbook lines 41-43 and 139.
- **Description:**
  - A T-SQL syntax error fails at parse time, so nothing is ever published by it.
  - Semantic errors are already rejected by the procedure and its constraints.
  - The dangerous case is a valid but wrong value, such as 15 instead of 1.5. Forms do nothing about it, and the concept removes the external reviewer who checked the Ratio.
  - "Failed" and "wrong" publish are undefined. For example, it is unclear whether an expected 50003 counts.
  - At about one change a month, "zero" cannot be told apart from today without a baseline.
- **Recommendation:**
  - Take a baseline from recent tickets.
  - Define countable failure and wrong-publish criteria, for example corrective versions as in CUJ 5.
  - Measure wrong publishes from any cause, and time from draft to live.
  - Classify procedure failures for telemetry (see M9).

### B8. The login, secret and data model are undefined
- **Lenses:** implementation-readiness (Blocker), codebase-details (Blocker), comprehension, design, structural, zoom-out.
- **Scope:**
  - `grant-permissions.sql:40-41`: the publisher has no SELECT, so it cannot pre-fill a draft, compute a diff or run the reconciliation lookup.
  - `grant-permissions.sql:25-29`: the script handles exactly two users.
  - `DependencyInjection.cs:21-25`: one `"Database"` connection string and one DbContext.
- **Description:**
  - Reusing `AddInfrastructure` as it stands gives the web app either the API login's rights, including INSERT/UPDATE on `dbo.Policies`, or only the publisher's.
  - There is no schema for change requests, revisions, comments or diffs.
  - "Append-only audit" conflicts with "drafts are edited" unless mutable state is split from immutable events.
- **Recommendation:**
  - Create a new login and a schema (for example `rulesadmin`) with:
    - SELECT on the pricing tables;
    - INSERT and SELECT only on revision and audit tables, with DENY UPDATE, DELETE as in lines 44-46;
    - EXECUTE on the wrapper only.
  - Make every draft save insert a new immutable revision, so the audit trail is the data itself.
  - Draft rule columns mirror `pricing.PricingRuleInput` exactly: the BIN2 collation and the precisions.
  - Add a second named connection string and a separate registration (for example `AddRulesAdminInfrastructure`).
  - Choose the credential type and where its secret is stored.
  - Choose between a new variable in `grant-permissions.sql` and a separate grant script.
  - Update `verify-permissions.sql`. Its Problem check covers only the `pricing` schema.
  - Update `PermissionTests.cs`.

---

## Majors

### M1. Interactive dry runs block live quotes
- **Lenses:** design (Blocker), inventive-catalyst, codebase-details. Synthesizer verified it.
- **Scope:**
  - Migration lines 251-297: a dry run really inserts version N+1 and its rules, then rolls back.
  - Design lines 393-397 and 501-504: the database runs plain locking READ COMMITTED.
- **Description:**
  - While the transaction is open, the quote query `TOP (1) ... ORDER BY VersionNumber DESC` waits on the uncommitted row.
  - A form that previews the diff on save or keystroke turns a rare stall into a recurring one, and a slow connection holding the transaction makes it worse.
  - A dry run also briefly reserves N+1, which can collide with a real approval.
  - A dry run cannot diff a stale draft: it raises 50003.
  - Each dry run needs a unique, non-blank `ChangeTicketRef`. A preview after the request has published fails with 2601.
- **Severity:** downgraded to Major because, at a handful of users, the stall per call is milliseconds. It is still a must-fix design rule.
- **Recommendation:**
  - Compute interactive diffs in C# from read-only SELECTs, using the same Name-matching logic as migration lines 270-293.
  - Call the procedure's dry run only at submit and at approve.
  - Set lock and command timeouts.
  - Monitor quote p95 latency during dry runs and publishes.
  - Optional: a read-only `pricing.ValidateRuleSet` procedure that checks the table parameter without inserting.

### M2. The approver must not be any contributor, and must approve exactly the content that publishes
- **Lenses:** design, comprehension, implementation-readiness, inventive-catalyst.
- **Scope:** the concept's separation-of-duties rule.
- **Description:**
  - A dual-role manager can co-edit or rebase a draft and then approve it.
  - Content can change between viewing and clicking.
  - Role claims are fixed for the life of a circuit or cookie.
- **Recommendation:**
  - Freeze content at submission.
  - Approval references a content hash, and publish sends exactly that payload.
  - Every editor and rebaser counts as a contributor and cannot approve.
  - Re-check the role and the `oid` on the server at the moment of approval.
  - Use a single-tenant app registration.
  - Consider re-authentication for Approve (`max_age`), or a Microsoft Graph role re-check.
  - The UI explains why Approve is disabled.

### M3. The approver loses the Ratio review, the only check against value typos
- **Lenses:** zoom-out, design, inventive-catalyst.
- **Scope:** runbook lines 41-43; Future Improvements at design lines 957-960.
- **Recommendation:**
  - The approval screen shows the dry-run Ratio and Change for each rule, recomputed against the current latest version at approval time.
  - Add a ratio-band gate (for example outside 0.8-1.25): the approver retypes the value, or the drafter gives a reason.
  - Add the planned policy-impact scan.
  - Run the catch-all-rate query (runbook lines 77-83) after each publish.

### M4. The diff is undefined and shows only multipliers
- **Lenses:** comprehension, zoom-out, structural, design, codebase-details.
- **Scope:** migration lines 270-292.
- **Description:**
  - The procedure marks a rule `Changed` for priority or condition edits but shows only the old and new multiplier.
  - Matching is by `Name`, so a rename shows as Removed plus Added, which can hide a multiplier change.
  - It is undefined whether the diff is taken against the base version or the latest, and when it is frozen into the audit.
- **Recommendation:**
  - Make the diff field-level for every rule column, against the base version, recomputed at approval, and frozen into the approval event.
  - Show renames explicitly, either through a stable rule key per draft or by forbidding renames.

### M5. Form validation must exactly match the database, from one source
- **Lenses:** codebase-details, zoom-out, structural, implementation-readiness, design, comprehension.
- **Scope:**
  - Migration lines 80-83, 140-160 and 235-245.
  - `PersistenceConventions.cs:7-12`.
  - `PolicyTypeParser.cs:12-17` (internal).
- **Description:**
  - A third copy of the rules will drift from the procedure and constraints.
  - Rule `Name` has no explicit collation (`PricingRuleConfiguration.cs:30`), so uniqueness follows the database default, which is probably case-insensitive. An ordinal check in C# would accept "Default" and "default", and the procedure would then reject them.
  - The policy-type list already lives in three CHECK constraints, the enum and `PersistenceConventions`.
- **Recommendation:**
  - Put advisory validation in one Domain type (for example `RuleSetDraft.Validate()` returning `Result`).
  - Generate dropdowns from the `PolicyType` enum.
  - The procedure stays the final authority.
  - Add a parity test that runs every row of the error table through both C# and SQL on LocalDB.
  - Map each error to a field-level message: 50001-50005, 547 by constraint name, and 2601 by index name.

### M6. Silent decimal rounding and late string truncation
- **Lenses:** codebase-details.
- **Scope:**
  - Table type at migration lines 196-201.
  - `RuleSetPublisher.cs:38-68`: untyped `DataTable` and `AddWithValue`.
- **Description:**
  - The server converts table-parameter decimals to `decimal(9,4)` and `decimal(18,2)`. A value such as 1.23456 can be stored as 1.2346 without an error.
  - Names longer than 200 characters and policy types longer than 32 fail only at publish time.
  - Verify the exact behaviour with a test.
- **Recommendation:**
  - Form validation:
    - at most 4 decimal places on Multiplier, with a maximum of 99,999.9999;
    - at most 2 decimal places on coverage limits;
    - length limits of 200 for Name and 32 for PolicyType.
  - Use `SqlMetaData` with explicit precision and scale.
  - Reuse `Policy`'s `decimal.Round(v, 2) != v` check.

### M7. Identity recording: `ApprovedBy`, the drafter and `ChangeTicketRef` format
- **Lenses:** all seven.
- **Scope:**
  - Migration lines 53-54: `nvarchar(64)` unique, and `nvarchar(128)` free text.
  - `db/tickets/0001-initial-rule-set.sql`.
- **Recommendation:**
  - Write `ApprovedBy` as `oid|upn` (fits in 128 characters). Keep the display name and the drafter's `oid` in the app audit, because the versions table has no drafter column.
  - Give change-request IDs a fixed prefix that cannot collide with legacy ticket references, for example `GFA-{guid}` (40 characters).
  - One change request maps to at most one version.
  - Document the new meaning for readers of legacy `ApprovedBy` values.

### M8. The fate of the ticket path, and the meaning of the active-version endpoint
- **Lenses:** structural, design, implementation-readiness, codebase-details, comprehension.
- **Scope:**
  - `GET /api/pricing/active-version` (design lines 532 and 604-605) now returns an app ID instead of a ticket number.
  - `db/templates/publish-rule-set-version.sql` and `db/tickets/0001-initial-rule-set.sql`.
- **Recommendation:**
  - Decide whether to retire the ticket path or keep it as an alerted break-glass path.
  - Alert on any version whose `ChangeTicketRef` does not match the app's format, so out-of-band publishes are detected.
  - Restate the endpoint's meaning in both docs and add a deep link from a version number to its change request.
  - Give the template and the first ticket file an explicit fate.
  - Define how app drafts behave when a ticket publish lands.

### M9. There is no observability foundation for the new workflow
- **Lenses:** codebase-details.
- **Scope:**
  - Design line 953: OpenTelemetry is only a Future Improvement.
  - Logging today is `LoggerMessage` only.
- **Recommendation:**
  - Emit one structured event per state change, with ChangeRequestId, base and new version, drafter and approver `oid`, SQL error number, constraint name and elapsed milliseconds.
  - Add a trace span around the publish call.
  - Add these metrics:
    - outcomes by class (validation escaped the form / concurrency / idempotency);
    - approve-to-commit latency;
    - open drafts on a stale base.

### M10. Approving now also sets go-live time, and there is no confirmation or undo help
- **Lenses:** design, inventive-catalyst, zoom-out (Nit).
- **Scope:**
  - Runbook lines 44-45: "run it when it should apply".
  - Decision 4, and Alternatives J and K (no effective dates).
- **Recommendation:**
  - Use a two-step confirmation that shows the version number about to be created and the diff against the latest.
  - State plainly that approving makes the change live now.
  - Offer a ready-made corrective draft: "republish version N-1 as a new version".
  - Decide whether that corrective draft gets a fast track that still keeps four eyes.
  - Do not add timer-based publishing.

### M11. Host, authentication and render mode are unspecified
- **Lenses:** implementation-readiness, structural, comprehension (Nit), design, zoom-out (Nit).
- **Scope:**
  - `Program.cs:23-28`: API key is the default scheme, with a fallback policy.
  - Design lines 1325-1326: "not a domain entity, because the API never creates versions".
  - Design Alternative P (no identity provider confirmed).
- **Description:** Adding OIDC to `Goldfish.Api` would turn 401 responses for API callers into 302 redirects.
- **Recommendation:**
  - Use a separate host (for example `src/Goldfish.PricingAdmin`) built on Microsoft.Identity.Web, and leave the API's authentication unchanged.
  - Decide whether a draft aggregate goes in `Goldfish.Domain`.
  - Name the app-role values (for example `Pricing.Analyst` and `Pricing.Manager`).
  - Specify the `oid` claim type, the tenant, the app-registration owner, the credential type, and the redirect URIs per environment.
  - Pin the package version. Microsoft.Identity.Web does not follow the 10.0.15 convention.
  - Choose the render mode. Static SSR with antiforgery is the recommended choice for the Approve action.

### M12. The testing strategy does not cover the new surfaces
- **Lenses:** implementation-readiness, codebase-details.
- **Scope:**
  - LocalDB tests run as sysadmin, which hides permission and ownership-chaining failures.
  - `LocalDbFixture.ResetAsync` is at lines 55-71.
- **Recommendation:**
  - Add a fake authentication scheme that injects `oid` and role claims.
  - Run tests under the real logins, using the `PermissionTests` pattern of `EXECUTE AS USER` / `WITHOUT LOGIN`.
  - Write one test per state transition, covering at least:
    - self-approval by a dual-role user;
    - a stale approval;
    - two managers approving at once;
    - a crash after commit.
  - Add the parity test from M5.
  - Extend `ResetAsync` to the new tables.
  - CI stays on Windows.

### M13. The schema migration, rollout and rollback plan is missing
- **Lenses:** implementation-readiness, structural.
- **Scope:**
  - The existing migration's `Down` drops everything (migration lines 304-328).
  - Runbook lines 122-132 (rollout, and the ban on `Down` in production).
- **Recommendation:**
  - Do not add a foreign key from `PricingRuleSetVersions` to the app tables. Legacy rows would fail it, and it would mean altering an immutable table. Use the reference prefix instead.
  - Write a "never run `Down` in production" rule for the new migration.
  - Rollout order: migration, then grants, then verify, then the Entra registration, then deploy the app, then a smoke publish, then revoke the old access.
  - Reconcile requests stuck in Approved or Publishing after a binary rollback.
  - History screens outer-join to show legacy versions that have no change request.

### M14. Comments, rejection and correction journeys are under-specified
- **Lenses:** comprehension.
- **Scope:** CUJ 5 (design lines 191-199); runbook lines 47-51.
- **Recommendation:**
  - Say whether comments attach to the draft or to a rule row.
  - Make a reason mandatory on rejection.
  - Make comments immutable audit events.
  - Add a "correct or revert to version N" journey that creates a normal draft. No path bypasses approval.

### M15. Updates to the runbook and the new design doc
- **Lenses:** structural.
- **Scope:** runbook lines 3-4, 27-71 and 122-141.
- **Recommendation:**
  - Runbook:
    - Replace the publishing workflow with the in-app flow.
    - Add an app message and action column to the error table. 50003 and 2601 on VersionNumber both mean "rebase". 2601 on ChangeTicketRef means "already published: reconcile".
    - Update the audience, Rollout and Accepted Risks: the web app holds the publish credential, a single approval publishes, and one person can hold both roles.
    - Add "Roles and access" and "Audit trail" sections.
  - New doc: `docs/designs/pricing-rules-admin-app.md`, mirroring the existing template. Add Technical Plan subsections for:
    - Relationship to existing design;
    - Change-request lifecycle;
    - Identity and roles;
    - Audit data model and permissions;
    - Publishing adapter;
    - Concurrency and rebase;
    - Validation ownership.
  - CUJs to cover:
    - the happy path;
    - self-approval refused;
    - a stale draft, then rebase, then re-approval;
    - a constraint error at approval;
    - a publish timeout followed by reconciliation;
    - a rejection with a comment;
    - correcting a published mistake.

---

## Nits

- **N1.** Cost and load are negligible: a few users, about 12 publishes a year, and a few hundred KB of snapshots. Record this, and run a single instance so SignalR sticky sessions are not needed. (design)
- **N2.** Add a staleness banner: show the base and latest version on every draft and in the approval queue, and notify drafters when their draft goes stale. (comprehension, inventive-catalyst)
- **N3.** State that a new draft starts as a full copy of a chosen base version, with change markers on each row. (comprehension)
- **N4.** Base-price edits through `PUT /api/policies` stay unreviewed. Say explicitly that they are out of scope. (comprehension; design line 937)
- **N5.** State HTTPS, antiforgery and the network placement explicitly. "Internal network only" is not a control. (implementation-readiness)
- **N6.** Retention of drafts, comments and diffs is still open (design lines 137-138). (implementation-readiness, codebase-details, design)
- **N7.** The existing design links `../../concepts_brief.md`. This concept's brief needs a distinct name so that link does not repoint. (structural)
- **N8.** Promote `RuleSetPublisher` into an Infrastructure adapter, and make it the only place that builds the table parameter. The column order is a fixed contract (migration line 192). (structural, codebase-details, zoom-out)
- **N9.** Consider tamper-evident audit rows, either hash-chained or a ledger table. Alternative Q rejected ledger tables for pricing rows, but the trade-off differs for an audit log. (design)

---

## Contradictions and Inventive Resolutions

| Contradiction | Resolution | Trade-off |
| --- | --- | --- |
| One-click publish vs. review that the database enforces | dbo-owned wrapper procedure; the app login has EXECUTE on the wrapper only (B3) | One more procedure to test; the `oid` is still supplied by the app |
| Audit owned by the app vs. a publish that commits its own transaction | Immutable revisions; the approval event and the publish share one outer transaction in the wrapper (B4) | Nested-transaction subtleties; never dry-run inside it |
| Rich live feedback vs. quote latency | Validation and diff in C# on SELECT-only data; the authoritative dry run only at submit and approve (M1) | Two validators, so a parity test is required |
| Approval vs. go-live timing | Explicit "approve = live now" confirmation, or a separate release click; no timers (M10) | Discipline, or an extra state |
| Concurrent drafts vs. approval tied to reviewed content | Approve disabled when stale; one-click rebase by Name; always re-approve (B6) | Merge logic; slower in busy months |
| Long-lived session vs. immediate role revocation | Re-authenticate or re-check the role at approval (M2) | An extra prompt, or a Graph dependency |
| Governance continuity vs. approval in the app | Auto-create or link an external ticket carrying the app's ID, or get compliance sign-off (B1) | An integration, or a compliance conversation |

**Ideal final result (inventive-catalyst):**

- Analysts edit immutable revisions in a form that cannot produce an invalid set.
- A manager who is not a contributor approves one hashed revision.
- One database transaction records that approval and publishes exactly that revision.
- The app holds no credential that can publish without such a trail.

---

## Reuse Opportunities

- `pricing.PublishRuleSetVersion` with `@DryRun = 1`: the authoritative check and diff at submit and approve (migration lines 207-301).
- `C:\Goldfish\tests\Goldfish.IntegrationTests\Infrastructure\RuleSetPublisher.cs:26-88`: a working table-parameter, OUTPUT-parameter and dry-run caller.
- `C:\Goldfish\src\Goldfish.Application\Pricing\GetActiveVersion\` and `C:\Goldfish\src\Goldfish.Infrastructure\Pricing\SqlPricingStatusReader.cs`: the base version for new drafts and the staleness banner.
- `C:\Goldfish\src\Goldfish.Infrastructure\Persistence\Pricing\PricingRuleRecord.cs` and `PricingRuleSetVersionRecord.cs`: both are internal, and InternalsVisibleTo covers only the test project.
- `C:\Goldfish\src\Goldfish.Infrastructure\Persistence\PersistenceConventions.cs`: length limits, collation and the policy-type list.
- `C:\Goldfish\src\Goldfish.Domain\Abstractions\Result.cs` and `Pricing\PricingErrors.cs`: the error pattern.
- `C:\Goldfish\tests\Goldfish.IntegrationTests\Database\PermissionTests.cs` and `Infrastructure\LocalDbFixture.cs`: test patterns for the new login.
- Runbook error table, lines 55-69: the source for user-facing messages.

## Open Questions

1. Does compliance or change management accept in-app approval in place of the external change ticket?
2. Phase 1 authoring only, or the full approve-and-publish workflow?
3. Is the ticket path and the `goldfish_pricing_publisher` credential retired, or kept as break-glass?
4. Can this SQL Server use gMSA or managed-identity authentication? Is Entra authentication to SQL available?
5. Do the app tables go in the same database? Which schema, and who owns the migration?
6. Rebase: three-way merge, or "start again from latest"?
7. Static SSR or Interactive Server render mode?
8. What retention applies to drafts, comments and diffs?
9. What is the default database collation in each environment? It decides whether rule-name uniqueness is case-sensitive.
10. Does a corrective republish of N-1 get a fast-track approval path?
11. What is the baseline rate of failed or reworked ticket scripts today?
