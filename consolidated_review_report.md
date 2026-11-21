# Consolidated Review: Policy Pricing Rules

* **Target:** `docs/designs/policy-pricing-rules.md` (1,589 lines; FeatureRange 1–790, DetailedRange 791–1589)
* **Context given to reviewers:** `concepts_brief.md`
* **Mode:** Consolidated report, all rubrics, uninterrupted
* **Date:** 2026-11-21

| Stage | Report | Reviewers | Raw result |
| --- | --- | --- | --- |
| 5 Initial Zoom Out | [zoom_out_report.md](zoom_out_report.md) | zoom-out, inventive-catalyst | 1 Blocker, 9 Major, 4 Nit |
| 6 Comprehension | [comprehension_report.md](comprehension_report.md) | comprehension | 0 Blocker, 2 Major, 9 Nit |
| 7 Critic | [critic_report.md](critic_report.md) | design, structural, style | 0 Blocker, 14 Major, 27 Nit |
| 8 Intermediate Zoom Out | [intermediate_zoom_out_report.md](intermediate_zoom_out_report.md) | zoom-out | 1 Blocker, 9 Major, 3 Nit |
| 9 Implementation Readiness | [implementation_readiness_report.md](implementation_readiness_report.md) | implementation-readiness, codebase-details, style | 1 Blocker, 14 Major, 13 Nit |

## Verdict

The design is not ready to implement. Two blockers stop it.

**Blocker 1: the publish procedure can silently undo an approved change.** Nothing ties a new version to the version it was written from.

**Blocker 2: the procedure's interface is not defined.** It has no parameter types and no error contract.

Every stage agreed the core of the design is sound and matches the brief:
* Each version is a full copy of the rule set.
* Rows are only ever added, never changed.
* Rules are read on every request, with no cache.
* Conditions use typed columns.
* Each quote stores a snapshot of the values it was priced from.
* The existing layers of the solution are kept.

The codebase checks passed: every file path and every claim about the existing code is accurate.

Stage 8's main conclusion runs through the other stages. Most findings are fixed by **removing** something, not adding it. Stage 9 confirmed that following Stage 8 makes 6 of its own findings disappear and shrinks 3 more.

## Decisions the author must make first

Several action items depend on these choices. Each one either reverses an earlier author decision or answers a question that is still open.

1. **Where matching runs (C-M2).** Moving it to C# reverses decision 6. Stages 5 (zoom-out), 8 and 9 favor C#. In Stage 5, inventive-catalyst defended SQL. Its reasons (clock and collation) were weakened when decision 4 removed effective dates.
2. **Whether policies can be updated (C-M3).** Dropping `PUT` and the temporal table reverses decision 11 and turns "how are base prices corrected?" into an open question.
3. **Whether there is an identity provider (C-M5).** If there is, JWT replaces the API-key design from decision 9.
4. **What drives the timing requirement (C-M12).** If price changes are tied to dates, Alternative J (effective dates) comes back. If not, the requirement needs rewording.

### Author decisions (recorded 2026-11-21)

| # | Decision | Effect on action items |
| --- | --- | --- |
| 1 | **Matching stays in SQL** (decision 6 unchanged). | C-M2 becomes a set of fixes, not a move. Fix how `MatchRow` maps rows (nullable rule fields, `CAST(... AS bit)`, the exact SQL text, `SqlQuery` named consistently). Add `Latin1_General_BIN2` on both `PolicyType` columns. Keep the status reader. Edge-case tests stay on LocalDB, so CI must run on Windows. Give Alternative M a sentence explaining the trade-off. |
| 2 | **Keep policy create, read and update, and add controls** (decision 11 unchanged). | C-M3: add a `ModifiedBy` column filled from the caller's key name. Add `rowversion` with `If-Match` on `PUT`, so a stale update returns 412. Remove the "last write wins" risk. Keep "unreviewed base-price edits" as an accepted risk, now recorded with who made the edit. Fix the temporal configuration syntax (Stage 9 N4). |
| 3 | **API keys with five permissions** (decision 9 unchanged). | C-M5: add a fallback authorization policy and mark Products `[AllowAnonymous]`. Apply every handler fix from Stage 9 M9. State the key rules: 32 random bytes, HTTPS only, a restart to rotate. Give Alternatives P and "two roles" sentences explaining the trade-off. |
| 4 | **Pricing changes are never tied to a time.** | C-M12: reword the requirement to "once a change commits, the next quote uses it, with no cache delay", and use one word for it. Remove the manual-timing accepted risk. Reject Alternative J because no pricing change is tied to a date. |

## Action items

### 🔴 Blockers

**C-B1. Publishing a version can silently undo another approved change.**
* **How it happens:**
  1. Two ticket scripts are both prepared from version 1.
  2. The first script publishes version 2.
  3. The second script then publishes version 3, a full copy of version 1 with its own change.
  4. Version 2's change is gone, and every check passed.
* **Fix:** add a required `@BaseVersionNumber` parameter and set the new number to `@BaseVersionNumber + 1`. The existing unique index then rejects both a stale script and a second publish that races the first. Also:
  * add a pre-check that returns a clear "rebuild the script from the current version" error;
  * remove the SERIALIZABLE wording and the MAX read;
  * have the template fill in the base number;
  * add tests for a stale base and for two publishes racing on the same base.

**C-B2. The publish procedure's contract is not defined.**
* **What's missing:**
  * the definition of the table-valued parameter: its columns, types, nullability and order;
  * the parameter types and the blank-value rules;
  * how the new version number is returned (OUTPUT parameter, result set, or return value).
* **What's wrong:** the THROW numbers are mapped to steps that are not checks.
* **Fix:**
  * Name every constraint so it describes itself, for example `CK_PricingRules_Multiplier_Positive`.
  * Keep THROW only for what constraints cannot check: an empty rule set, the catch-all rule, and a stale base version.
  * Publish one table that lists each rejection, where it is enforced, its error number and its message.
  * Have test 66 assert those error numbers.
  * Make CUJ 4's wording match the table.

### 🟡 Major

**C-M1. Check changes before they go live.** The design's headline mistake (a multiplier of 15 instead of 1.5) has no check before customers see it.
* **Fix:** add `@DryRun bit` to the procedure. It runs every check, returns each rule's old multiplier, new multiplier and ratio against the base version (matched on `Name`, which becomes unique per version), and then rolls back. The output is attached to the change ticket.
* **Turn down** delta publishing, ratio thresholds, the full policy scan and the alert.

**C-M2. Do the matching in C#.** *(Author decision 1.)*
* **Fix:**
  * Load the latest version and its rules with one plain read, then pick the winner in a pure Domain function.
  * Reuse the same read for the active-version endpoint, and drop the separate status reader and its handler (files 23–25 and 39).
  * Set `Latin1_General_BIN2` collation on both `PolicyType` columns.
  * Use LocalDB only for the procedure and permission tests, and state the Windows CI trade-off.
* **Removes:** the `MatchRow` mapping bugs and the collation workaround.

**C-M3. Make policies create and read only.** *(Author decision 2.)*
* **Fix:**
  * Drop `PUT`, the temporal table, its history table and grant, CUJ 6, the history toggle in the test reset, and the two related accepted risks.
  * Move "how are base prices corrected?" into Open Questions.
* **If updates must stay:** add `ModifiedBy` and `rowversion`/`If-Match`, and fix the syntax of the temporal configuration.

**C-M4. Remove `READ_COMMITTED_SNAPSHOT`.**
* **Why:** rows are only ever inserted and each publish commits as one unit, so the default isolation level already prevents reading half a version.
* **Fix:** replace the setting and its deployment notes with one sentence saying this.

**C-M5. Settle authentication.** *(Author decision 3.)*
* **If there is an identity provider:** use the standard JWT handler.
* **If API keys stay:**
  * Use two roles, for example "pricing client" and "operator".
  * Add a fallback authorization policy, and mark `ProductsController` `[AllowAnonymous]`.
  * State that keys are 32 random bytes, that HTTPS is required, and that rotating a key needs a restart.
  * Fix the handler details:
    * limit key names to 1–64 characters;
    * set `ClaimTypes.Name`;
    * create the identity with authentication type `"ApiKey"`;
    * use settable options properties and initialize `Keys = new()`;
    * compare hashes with `Convert.FromHexString`;
    * read options through `IOptionsMonitor`;
    * deal with the nullable warning on `User.Identity!.Name`.

**C-M6. Simplify observability.**
* **Fix:**
  * Log catch-all quotes at Information.
  * Remove `QuoteMetrics`, its packages and `AddMetrics()`, which would not compile in the Application project anyway.
  * Add a runbook query over `pricing.Quotes`.
  * Split the quote handler into success and failure paths, with a log level and message template for each error code.
  * Reword accepted risk 1 so it no longer points at a metric.

**C-M7. Fill in the missing permissions and state G5's real limits.**
* **Fix:**
  * Add `GRANT EXECUTE ON TYPE::pricing.PricingRuleInput` for the publisher, and the API login's rights on `dbo.Products`.
  * Add both to the grant script, the verify script, test 71 and the spec's permissions table.
  * Rewrite G5 as "no login used by the API or by ticket scripts can change published rules".
  * Add the migrator login as an accepted risk.
  * State that the schema and its objects share one owner.

**C-M8. Make the grant script runnable from tests.**
* **Fix:**
  * Define the script's variables as user names.
  * Describe the test's loader (substitute the variables, split on `GO`), or call `sqlcmd -v` instead.
  * Test that running the script twice is safe, or remove the claim that it is.

**C-M9. Make the test infrastructure safe and well defined.**
* **Fix:**
  * Override the connection string with `UseSetting`, or replace `DbContextOptions` in `ConfigureTestServices`.
  * Guard that the database name starts with `Goldfish_Test_`.
  * Pin the xUnit and test SDK versions.
  * Define the fixture, collection and reset contract, deleting in the order Quotes, then Rules, then Versions, then Policies.
  * Resolve services in tests through `CreateScope()`.
  * List the test keys and their hashes.
  * Add a testing item to the goals or NFRs.

**C-M10. Split the work into phases.**
* **Fix:** six phases, each with a build and test command:
  1. Error types and the shared problem-details mapping.
  2. Policies.
  3. The pricing schema and the procedure.
  4. Matching and quotes.
  5. Authentication.
  6. Database scripts, the permission test and the runbook.

**C-M11. Make the verification gates runnable.**
* **Fix:**
  * Add `TreatWarningsAsErrors` and `coverlet.collector`, plus the commands to run them.
  * Add `--connection` to the `dotnet ef database update` step.
  * Point to the existing `dotnet-tools.json`.
  * Add a manual step that creates the CUJ 1 policy, and name the login that runs the ticket script.

**C-M12. Settle the timing requirement.** *(Author decision 4.)*
* **Fix:**
  * Add brief Q12 (what business event makes seconds matter) to Open Questions.
  * Use one word for the requirement throughout.
  * Give Alternative J a real reason for rejection.
  * Note that a change needed at a set time can run as a scheduled ticket script, for example a SQL Agent job.

**C-M13. Make the Alternatives section traceable.**
* **Fix:**
  * Correct the link to the brief to `../../concepts_brief.md`, and write every resource as a Markdown link.
  * Add the missing ownership-chaining URL.
  * For each "Rejected by author" alternative, add one trade-off sentence. The alternatives are P, Q, M, H, L and G.
  * Split Alternative T into separate entries.
  * Add the missing "most-specific match wins" alternative.
  * Explain in one line which review "Rejected in review" refers to.

**C-M14. Collect risks in one section.**
* **Fix:** create one H2 "Risks & Mitigations" section. Give each risk either a mitigation or an "accepted because…" line, and replace the scattered copies with pointers to that section.

**C-M15. Describe the publishing workflow.**
* **Fix:** add one paragraph covering who drafts the script, who approves it, who runs it, and with which login and tool.

**C-M16. Give rule rows an Id default.**
* **Fix:** add `HasDefaultValueSql("NEWSEQUENTIALID()")` to `PricingRules.Id`. Without it, the procedure's INSERT fails.

**C-M17. Estimate storage.**
* **Fix:**
  * Add the `PolicyId` index to the data model table, which currently omits it, although file 29 includes it.
  * Add a one-line yearly storage estimate at the assumed load.
  * Do not add partitions or a rate limit yet.

**C-M18. Tighten the wording that could be implemented wrongly.**
* **Fix:**
  * Always say "priority number" when comparing priorities.
  * Say "has been published", not "has started".
  * Name the only way a quote can find no matching rule: rows written outside the procedure.
  * Correct the collation claim.

**C-M19. Pin package versions consistently.**
* **Fix:** pin every package at 10.0.15, and decide whether `Microsoft.Data.SqlClient` gets its own reference.

### 🟢 Nits (grouped)

**C-N1. Structure.**
* Make Additional Resources and Future Improvements H2 sections.
* Use horizontal rules consistently.
* Make the decision-table rows match their column header.
* Give each column its own row in the data model tables.
* Give every API error cell both a status code and an error code.
* Add a short CUJ for "no valid key" and one for "no version yet".

**C-N2. Design accuracy.**
* Add a composite foreign key on (VersionId, RuleId).
* Reword G4 to "needs only the immutable rule rows".
* Document that timestamps are for the record only.
* Make `Down` optionally throw when data exists.
* Say who confirmed banker's rounding, and use one name for it.
* State how the API behaves while SQL Server is down.
* Use one name for the catch-all flag (`WasCatchAll`).
* Define "more than 2 decimal places", and set an upper bound on amounts.
* Name the UTC converter, the record property types and the foreign keys.
* Add a CHECK constraint on `Quotes.PolicyType`.

**C-N3. Code conventions.**
* Use `CancellationToken cancellationToken = default`.
* Use `AsNoTracking` by default.
* Check `public partial class Program;` against ASP0027.
* Use the HTTPS profile in `Goldfish.Api.http`.
* Put literal values in the ticket template.
* Supply the PowerShell key one-liner.

**C-N4. Wording.**
* Remove the promotional and filler words that Stages 7 and 9 listed (N16–N27 and N13).
* Add decision 13 (LocalDB for tests) or drop the reference to it.
* Explain in one clause each the existing patterns the design assumes.
* Say "authorization policy" when that is what you mean, as opposed to an insurance policy.
* Use the `ApiKeys:Keys` configuration shape everywhere.
* Trim code-level detail out of the Technical Plan.
* Drop the copies of rule fields on quotes, or explain why they are there.

## What this removes and adds

If the four author decisions follow the reviewers' recommendations (C# matching, no policy updates, no RCSI, no metrics), then:

* **Removed:** about 12 planned files, the temporal table, a database-wide setting, two package references and two accepted risks.
* **Added:** one new mechanism, `@DryRun`, plus `@BaseVersionNumber`, the error table, test phases and a few grants.

## Traceability matrix

| Consolidated item | Raw findings |
| --- | --- |
| C-B1 | S5 C1; S7 N7; S8 Z1; S9 M14 |
| C-B2 | S6 Major 2; S8 Z9; S9 B1 |
| C-M1 | S5 A2, C2, C3; S7 M4; S8 Z2 |
| C-M2 | S5 B1, B7, A4; S7 M5 (Alt M), M7, M13; S8 Z5; S9 M2, N6, N8 |
| C-M3 | S5 A1; S7 M8 (partial); S8 Z3; S9 M6 (partial), N3, N4, N12 |
| C-M4 | S5 A3; S7 M2 (partial); S8 Z4; S9 M10 |
| C-M5 | S5 B4; S7 M5 (Alt P), M10, N12; S8 Z6; S9 M9 |
| C-M6 | S5 B2, B3; S7 N11; S8 Z8; S9 M7 (partial), M11 |
| C-M7 | S7 M2, M9; S8 Z11; S9 M3 |
| C-M8 | S9 M5 |
| C-M9 | S7 M7; S9 M4, M6 |
| C-M10 | S9 M1 |
| C-M11 | S9 M13 |
| C-M12 | S7 M3; S8 Z10 |
| C-M13 | S6 Nit (review/brief); S7 M1, M5, M6 |
| C-M14 | S7 M8 |
| C-M15 | S6 Major 1; S8 Z11 |
| C-M16 | S9 M8 |
| C-M17 | S7 M11; S8 Z7 |
| C-M18 | S6 Nits (started, priority wording); S7 M12, M13 (text), M14; S9 M12 |
| C-M19 | S9 M7 (pinning) |
| C-N1 | S7 N1–N6 |
| C-N2 | S5 B6; S7 N8–N10, N13–N15; S8 Z12; S9 N1, N5, N7 |
| C-N3 | S9 N2, N3, N9, N10, N11 |
| C-N4 | S5 B5; S6 Nits (patterns, callers, policy wording, config shape, link); S7 N16–N27; S8 Z13; S9 N13 |

## Correction

Stage 9 M13 states that there is no tool manifest for dotnet-ef. In fact `dotnet-tools.json` at the repository root already pins dotnet-ef 10.0.15, so the plan only needs to point to it.
