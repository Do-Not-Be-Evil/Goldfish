# Pricing Rules Admin Web App

* **Author:** Do-Not-Be-Evil
* **Date:** 2026-11-21

## Overview

Business analysts change the insurance pricing rules about once a month. Today they hand-write a SQL change-ticket script from `db/templates/publish-rule-set-version.sql`, a reviewer approves the ticket, and someone runs the script with sqlcmd under the `goldfish_pricing_publisher` login. The author reports that syntax mistakes in those scripts are the main pain.

The concept is an internal .NET 10 Blazor web app, added to the Goldfish solution:

* Analysts sign in with Entra ID and build a rule-set change in forms, starting from the live rules, instead of writing SQL.
* They submit the change as a draft.
* A manager (Entra app role) who is not the drafter approves it. One approval publishes the new version at once, through the existing `pricing.PublishRuleSetVersion` procedure.
* Approval moves out of the external change-ticket system and into the app. The app's change-request ID goes into each version's `ChangeTicketRef`.
* The app keeps the audit trail: drafts, submissions, approvals, rejections, comments and diffs.

The full reviewer findings (8 Blockers, 15 Majors, 9 Nits) are in [rules_admin_brainstorm_findings.md](../../rules_admin_brainstorm_findings.md). This brief summarizes them.

## Analysis

### The concept is two changes, and only one of them fixes the stated pain

1. **Forms instead of hand-written SQL.** This removes syntax mistakes and the retyping of the current rules.
2. **Approval and publishing inside the app, with the web app holding a login that can change live prices.** This is a governance and security change. The stated pain does not require it.

Change 2 also reverses the current design. `docs/designs/policy-pricing-rules.md` lists "a rule-editing screen or in-app approval workflow" as a Non-Goal and rejects it as Alternative U. The design's goals G5/G6, CUJs 3, 4, 5 and 7, and the runbook's publishing steps all assume ticket review. A new design must state which of these it keeps, changes or withdraws. Change management has accepted in-app approval in place of the ticket (reported by the author, 2026-11-21).

### The real risk is a valid but wrong value, not a syntax error

A T-SQL syntax error fails before anything runs, so it never publishes a wrong price. The procedure and the table constraints already reject invalid rule sets. The dangerous case is a value that passes every check but is wrong, such as a multiplier of 15 instead of 1.5. Forms do nothing about that. Today the ticket reviewer is told to check the dry-run Ratio column, which is the one control against it. The concept must keep an equivalent check on the approval screen. Options include showing the Ratio and the change for every rule, and requiring a retype or a reason when a ratio falls outside a set band. The success measure "zero failed publishes from syntax mistakes" is already met today, so it needs replacing. Candidates are wrong publishes from any cause (counted as corrective versions) and time from draft to live, measured against a baseline taken from recent tickets.

### "The drafter cannot approve" is enforced only in app code

The procedure checks only that `@ApprovedBy` is not blank, and the publisher login can execute it with any value. If the web app holds that login, anything that can act as the app can publish any rule set under any approver name with no second person. That covers an app compromise, a developer with production configuration, an authorization bug, or a wrong Entra role assignment. The reviewers' resolution:

* Give the app its own database login. It has no EXECUTE on the inner procedure.
* Grant that login EXECUTE only on a new dbo-owned wrapper procedure, for example `rulesadmin.ApproveAndPublish(@RevisionId, @ApproverOid, ...)`.
* The wrapper reads the rules from a stored, frozen, submitted revision; the app never passes rules on the approve path. It then:
  * checks that the approver is not any contributor;
  * checks the content hash;
  * checks that the request has not already been published;
  * calls the inner procedure through ownership chaining.

The approver's identity still comes from the app. The wrapper makes any forgery leave a matching draft, submission and approval trail in the database. Only Entra authentication to SQL Server would remove that last gap, and it is unconfirmed for this environment.

### Publishing and auditing must not be two separate writes

The procedure commits its own transaction. If the app publishes and then writes "Published" to its audit tables separately, three things can go wrong:

* A crash between the two writes leaves a live price change with no audit record.
* A lost commit acknowledgement plus a retry (a double-click or a manual retry) returns error 50003 or 2601 for a change that is already live. The analyst may then republish it.
* Wrapping the call in an app transaction does not help either. The dry-run path's ROLLBACK kills the outer transaction.

The resolution is to record the approval event in the same transaction as the publish, inside the wrapper, and never dry-run inside that transaction. Failing that, treat the change-request ID as an idempotency key and look it up before recording any outcome.

### Undefined parts that the design must settle

* **Lifecycle.** The states (Draft, Submitted, Publishing, Published, Rejected, NeedsRebase, Withdrawn) need defining, along with who may trigger each transition and what each failed approval leaves behind. Two managers approving at once must resolve to one winner.
* **Rebase.** If rebase only re-points a stale draft at the newest version, it silently undoes the change published in between. Two safe options:
  * a three-way merge by rule `Name`, with the rebased draft always going back for fresh approval;
  * no merge at all: "start a new draft from the latest version". This is simpler, and enough at about 12 publishes a year.
* **Separation of duties.** Every person who edited or rebased a draft counts as a contributor and cannot approve it. Approval covers one frozen, hashed revision. The role and user ID are re-checked on the server at the moment of approval.
* **Data model.** Every draft save is a new immutable revision, so the "append-only" audit is the data itself. The app tables get DENY UPDATE/DELETE, as the pricing tables do. Draft columns mirror `pricing.PricingRuleInput` exactly: binary collation, `decimal(9,4)` and `decimal(18,2)`.
* **Diff.** Show every rule field, not just the multiplier, against the base version, recompute it at approval, and freeze it into the approval record. A rule rename currently shows as one removed and one added rule, which can hide a multiplier change.
* **Quote latency.** A dry run really inserts version N+1 and then rolls it back, and live quote queries wait on that row. Live previews in the form must compute the diff in C# from read-only queries. The procedure's dry run runs only at submit and at approve.
* **Validation.** Form checks live in one Domain type. A parity test runs every error case through both C# and SQL. Rule-name uniqueness follows the database's default collation, which is probably case-insensitive, and the C# check must match it. Table-parameter decimals can be rounded silently, so the form must enforce 4 decimal places on the multiplier and 2 on coverage limits.
* **Hosting.** Use a separate host project, for example `src/Goldfish.PricingAdmin` on Microsoft.Identity.Web. Adding sign-in redirects to `Goldfish.Api` would turn API 401 responses into 302 redirects. Use a second connection string and its own registration; the current `AddInfrastructure` has one connection string.

### What the concept gets right

* It reuses the stored procedure as the single path to publish, so the database-side checks (stale base, one catch-all, constraints) stay the final authority.
* It uses real Entra user identities instead of API-key names for people.
* Load is negligible: a few users and about 12 publishes a year. One app instance is enough.

### Ideal final result

Analysts edit immutable revisions in a form that cannot produce an invalid set. A manager who did not contribute approves one hashed revision. One database transaction records that approval and publishes exactly that revision. The web app holds no credential that can publish without such a trail.

## Goals vs. Non-Goals

* **In-Scope Goals:**
  * Analysts build rule-set changes in forms that start from the live rules, with field-level validation that matches the database.
  * Analysts and managers see a full field-level diff and the Ratio for every rule before submitting and before approving.
  * A manager who did not contribute to a draft approves it, and that approval publishes it at once.
  * Every draft revision, submission, approval, rejection, comment and diff is kept and never changed.
  * The database enforces the approval rules, not only the app.
  * Live quotes are not slowed by drafting.
* **Out-of-Scope Non-Goals:**
  * Future-dated or scheduled publishing (Decision 4 and Alternatives J/K of the existing design stay).
  * Editing base prices; `PUT /api/policies` stays outside this review flow.
  * Changes to the quote endpoint or the matching logic.
  * Multi-step or multi-approver workflows.
  * Access from outside the internal network.

## Alternatives Weighed

* **Keep the external change ticket as the approval; the app only builds and validates the script or dry run.** The reviewers' preferred Phase 1. It fixes the stated pain with no new credential exposure and no workflow engine. Rejected by author, and reconfirmed after the Goldfish review: approval stays inside the app. No reason has been recorded yet; one is needed before drafting, because the existing design rejected the opposite choice (Alternative U).
* **Approval and publishing as two separate steps (approve, then someone presses publish).** Rejected by author: one manager approval should publish immediately. The trade-off is that approving also picks the go-live moment, so the approval screen must say plainly that approval makes the change live now.
* **People identified by API keys, as the API does today.** Rejected by author in favour of Entra ID: separation of duties needs individual identities, not machine key names.
* **Adding sign-in to the existing `Goldfish.Api` host.** Not chosen: OpenID Connect redirects would change the API's 401 responses. A separate host keeps the API's authentication unchanged. (Reviewer recommendation; the author has not confirmed it.)
* **Live dry runs on every form edit.** Not chosen: each dry run briefly holds a lock that live quotes wait on. Diffs are computed in C# instead. (Reviewer recommendation.)

## Key Decisions & Staging for Next Steps

**Decided by the author:**

1. The main users are analysts drafting rule changes; the main pain is syntax mistakes.
2. Approval happens inside the app and replaces the external change ticket for pricing changes.
3. The app calls the publish procedure itself.
4. Approvers are managers. A drafter can never approve their own change.
5. Sign-in is Entra ID.
6. One manager's approval publishes immediately.
6a. Change management accepts in-app approval in place of the external change ticket (author, 2026-11-21; sign-off ticket `CHG-TBD`).
7. The database enforces the approval rule, not only the app. The web app gets its own login with EXECUTE only on a new dbo-owned wrapper procedure (for example `rulesadmin.ApproveAndPublish`). The wrapper:
   * reads a frozen, submitted revision from the database;
   * checks that the approver is not any contributor, that the content hash matches, and that the request is not already published;
   * writes the approval event in the same transaction as the publish.

   The app never sends rules on the approve path, and the app login cannot execute `pricing.PublishRuleSetVersion` directly.
8. The `goldfish_pricing_publisher` login is revoked, and the sqlcmd ticket-script path is retired. The wrapper is the only route to publish. Consequences the design must cover:
   * There is no emergency path. If the app is down, nobody can publish a correction except a DBA with sysadmin rights, outside every control. The design needs a documented, audited procedure for that case.
   * `db/templates/publish-rule-set-version.sql`, `db/tickets/`, the runbook's publishing section, `grant-permissions.sql`, `verify-permissions.sql` and `PermissionTests` all change.
   * Add an alert for any version whose `ChangeTicketRef` lacks the app's prefix, which would reveal a publish made outside the wrapper.
9. The approval screen shows the Ratio (new multiplier divided by old) and the change for every rule. If any rule's ratio falls outside a set band, the draft cannot be submitted without a written reason, and the manager sees that reason beside the rule. Details for the design:
   * **Band:** assumed 0.8 to 1.25, the reviewers' example. The author has not confirmed it. The design must say whether the band is fixed or configurable, and who may change it.
   * **Database enforcement:** to stay consistent with item 7, the wrapper also refuses to publish a revision with an out-of-band rule that has no reason.
   * **Added and removed rules:** a new rule has no old multiplier, so it has no ratio, and the design must decide whether it always needs a reason. The same goes for a removed rule.
   * **Audit:** the reason is stored with the frozen revision and appears in the audit trail.

**Assumed, not yet confirmed by the author:**

* Roles come from the Entra app roles `Pricing.Analyst` and `Pricing.Manager`. One person may hold both; the approval check compares user IDs.
* `ChangeTicketRef` holds the app's change-request ID, with a fixed prefix such as `GFA-` so it cannot collide with old ticket numbers.
* The audit trail is append-only and kept in the same database.
* The app is a server-rendered Blazor app in a separate host project, internal network only.
* A stale draft cannot be approved and must be rebuilt on the latest version.

**Open questions to settle before `/egm:egm-draft`:**

1. *(Settled: change management accepts in-app approval, reported by the author on 2026-11-21.)* The sign-off is recorded under a change ticket; its number (`CHG-TBD`) still needs adding so the design can cite it. The design must still state why it overturns Alternative U of the existing design.
2. *(Settled by item 1: the full approve-and-publish workflow is in scope, not an authoring-only Phase 1.)*
3. *(Decided, see item 8: the publisher login is revoked and the ticket path retired.)*
4. *(Decided, see item 7: the database enforces approval through the wrapper.)*
5. Rebase: a three-way merge with re-approval, or "start a new draft from latest"?
6. *(Decided, see item 9: show the Ratio, and require a reason outside a band.)* Confirm the band limits (0.8 to 1.25 assumed).
7. What success measure and baseline replace "zero syntax-error publishes"?
8. Can this SQL Server authenticate the app with a gMSA, a managed identity or Entra ID?
9. How long must drafts, comments and diffs be kept?
10. Static server rendering or interactive server rendering?


## Implementation notes (2026-11-22)

Built in `src/Goldfish.PricingAdmin` plus the `AddRulesAdmin` migration. The open questions above were settled
with these defaults; each can be changed.

| Question | Default implemented |
| --- | --- |
| Rebase | No merge. **Restart from version N** replaces a stale draft's rules with the latest published rules as a new revision; the analyst re-enters the change and resubmits. A submitted draft must be rejected or withdrawn first. |
| Band limits | 0.8–1.25, stored in the single-row table `rulesadmin.RatioBand` (changed by a DBA or a migration; the app login cannot change it). New rules always need a reason; removed rules do not. |
| Content hash | Not used. Revisions are insert-only for every non-privileged login, so approval names a revision ID, and the wrapper refuses anything but the latest submitted revision. |
| Rendering | Static server rendering with antiforgery-protected form posts; no interactive circuit. |
| Database credential | Separate login `goldfish_rules_admin` (connection string `RulesAdmin`); the type (gMSA, managed identity or SQL login) is left to each environment. |
| Retention | Not set. Nothing is ever deleted. |
| Success measure and baseline | Not set. Corrective versions can be counted from the change requests and versions tables. |
| Dry run at submit | Not used: the app login cannot run the publish procedure. The form validator mirrors the procedure, and `RuleSetValidatorParityTests` keeps them in step. |
| Sign-off ticket | Still `CHG-TBD`. |

**Database enforcement.** `rulesadmin.ApproveAndPublish` (errors 51001–51007) locks the change request,
checks that the latest revision is submitted and not yet approved, that the approver did not create, edit or
submit it, and that every new or out-of-band rule has a reason, then calls `pricing.PublishRuleSetVersion`
and inserts the approval in the same transaction. The app login has EXECUTE on the wrapper only, INSERT and
SELECT on the history tables, and is denied UPDATE and DELETE everywhere, INSERT on approvals, and EXECUTE on
the publish procedure. The old publisher login is revoked and denied.

**Not built:** notifications, a policy-impact scan of a draft, OpenTelemetry tracing, and a fast-track
correction path.
