# Stage 5: Initial Zoom Out Review

* **Target:** `docs/designs/policy-pricing-rules.md` (lines 1–1589)
* **Context:** `concepts_brief.md`
* **Standard:** zoom-out-standard
* **Reviewers:** egm-zoom-out (completed, 11 raw findings), egm-inventive-catalyst (completed, 7 raw findings)

After merging duplicates there are **14 findings: 🔴 1 Blocker, 🟡 9 Major, 🟢 4 Nit.**

Both reviewers agree the core of the design fits the brief:

* Each version is a full copy of the rule set.
* Rows are only ever added, never changed.
* Rules are read on every request, with no cache.
* Conditions use typed columns limited to a fixed list of values.
* Each quote stores a snapshot of what it was priced from.
* The existing layers of the solution are kept.

## A. Raised by both reviewers

### 🟡 A1. Base-price edits skip every control that rule edits have

* **Reviewers:** zoom-out (Major), inventive-catalyst (Nit, close to Major).
* **Lines:** 174–181 (CUJ 6), 199, 240–249, 389, 477–482, 683–693, 786, 967–976, 1066–1080, 1452–1455.
* **Problem:** The price is base price × multiplier. The multiplier side has ticket review, append-only versions, permissions with explicit DENY, and a recorded ticket reference. The base-price side has none of these.
  * Any key with `policies:write` can change a policy's base price, type, or coverage limit, and the last write wins.
  * Changing the type or coverage limit also changes which rule matches.
  * The temporal history table records what changed but not who changed it.
* **Scope:** The brief asked only for policy tables and left open how policies are created (its Q7). The design still adds a full create, read, and update API, a temporal table, a history table, and a test reset that turns system versioning off and on.
* **Why it matters:** Pricing control is only half solved. Nobody can answer "why did this policy's price change?" in full. Meanwhile the design carries machinery nobody asked for.
* **Direction:** First decide where base prices really come from.
  * If no workflow needs to edit them through the API, ship create and read only, or load policies by ticket script, and drop the temporal table. That is Alternative R.
  * If edits are needed, either send them through the same ticket control or justify why they don't need it.
  * At a minimum:
    * add a `ModifiedBy` column, filled from the caller's key name, so the history records who made each change;
    * add optimistic concurrency (`rowversion` + `If-Match`) so updates can't silently overwrite each other.

### 🟡 A2. The main risk named in The Problem has no guard before it goes live

* **Reviewers:** zoom-out (Major), inventive-catalyst (Major).
* **Lines:** 44–48, 196, 473–475, 649–659 (Alternative O), 782–785.
* **Problem:** The Problem section uses "15 instead of 1.5" as its motivating example and says the database must reject clearly invalid values. Decision 8 and Alternative O then accept exactly that risk. Inventive-catalyst calls this a false choice between a fixed ceiling, which blocks discounts, and no guard at all. A typo is a surprising change from the live version, not an invalid value on its own.
* **Why it matters:** The failure the design was built to prevent goes live with no automatic barrier. It is caught only afterwards, through logs and a metric, once customers have already been mispriced.
* **Direction:**
  * At publish time, compare each rule's multiplier with the same rule in the version the script was based on.
  * Reject a change of more than a set ratio, for example 2× up or down, unless the script explicitly lists the rules it intends to move that far.
  * This depends on rules having a stable key from one version to the next (C2).
  * A lighter fallback: a read-only preview that shows each rule's new multiplier next to the live one (C3).
  * The cost: someone must own the thresholds, and the acknowledgement can become a box people tick by habit.

### 🟡 A3. `READ_COMMITTED_SNAPSHOT` changes the whole database for a benefit it doesn't need

* **Reviewers:** zoom-out (Major), inventive-catalyst (Nit). The synthesizer settled on Major because the change is database-wide and is never reversed.
* **Lines:** 377–379, 743–744, 756–758, 1150–1153.
* **Problem:** The migration switches the whole database to snapshot reads with `ROLLBACK IMMEDIATE`, to stop quotes reading a half-inserted version. Under the default isolation level that can't happen anyway: a quote waits a few milliseconds while a publish commits. Publishes add a few hundred rows about once a month.
* **Why it matters:**
  * It changes read behavior for the existing Products code.
  * It drops open connections when the migration runs.
  * The migration needs ALTER DATABASE rights.
  * `Down` never turns it off.
* **Direction:** Drop the setting. Rely on the single publish transaction and accept the short block. If a non-blocking read is ever needed, use `ALLOW_SNAPSHOT_ISOLATION` for the pricing read only.

### 🟢 A4. Choosing LocalDB makes CI depend on Windows

* **Reviewers:** both.
* **Lines:** 1444–1457, 1571–1572.
* **Problem:** The test-database decision forces Windows CI, and LocalDB behaves slightly differently from production SQL Server.
* **Direction:** Either state the trade-off explicitly, or let the test fixture use LocalDB or a Testcontainers SQL Server depending on the environment. If matching moved to C# (B1), far less would depend on the database at all.

## B. Zoom-out only

### 🟡 B1. The rule-matching business logic lives in raw SQL in Infrastructure

* **Lines:** 194, 309–352, 627–634 (Alternative M), 865–880, 1177–1191.
* **Problem:**
  * Precedence, the exclusive/inclusive boundaries, and what counts as a catch-all all live in a CTE with `OUTER APPLY`. The Domain gets back only a DTO.
  * "Catch-all" is defined in three places: the filtered index, the procedure check, and the query's CASE expression.
  * Matching in C# was rejected because it loads every rule, but at a few hundred rules and tens of quotes per second that cost is negligible.
* **Why it matters:** The core pricing logic can't be unit-tested, and every edge case needs LocalDB. In Clean Architecture, business rules belong in the Domain.
* **Direction:** Load the active version's rules with one plain query and pick the winner in a pure Domain function. If SQL matching stays, justify it with a measured cost.
* **Disagreement:** Inventive-catalyst looked at this and chose not to raise it. It sees SQL matching as a genuine resolution of the clock and collation conflicts. Zoom-out points out that the closed list of policy types already removes the collation concern. The author should decide explicitly.

### 🟡 B2. Normal catch-all pricing is logged at Warning

* **Lines:** 143–145, 191, 465–467, 588–590, 1012–1013.
* **Problem:** CUJ 2 treats the catch-all as the correct price for anything no specific rule covers, yet every such quote is logged at Warning as a "gap".
* **Why it matters:** If many quotes use the default, Warning logs fill with expected traffic and real problems get buried, which works against G7.
* **Direction:** Decide what the catch-all means.
  * If it is a legitimate default, log at Information and rely on the stored `WasCatchAll` flag.
  * If it is a safety net, say so and require rule sets to cover every intended policy explicitly.

### 🟡 B3. The metric has no exporter and duplicates stored data

* **Lines:** 466–467, 473–475, 779, 931–937, 991–999, 1052–1053.
* **Problem:** The design adds `QuoteMetrics`, `IMeterFactory`, two package references, and `AddMetrics`, but nothing exports the metric, because OpenTelemetry is listed as a future improvement. `pricing.Quotes` already stores the version and the catch-all flag on every row. The mitigation for A2 still relies on this metric.
* **Direction:** Remove the counter. Document a query over `pricing.Quotes`, grouped by version and catch-all flag, in the runbook. Add metrics together with OpenTelemetry later.

### 🟡 B4. The API key permission model is more detailed than any known caller needs

* **Lines:** 197, 434–450, 661–668, 1211–1245, 1310–1316.
* **Problem:** The design hand-builds an authentication scheme with five permissions, startup validation, and a rotation procedure, while who calls the endpoint is still unknown (brief Q14). JWT was rejected only because it needs an app registration.
* **Why it matters:** Hand-built authentication deserves heavy scrutiny. Five permissions with no known caller is speculation. If the organization already has an identity provider, this is a second credential system to run and audit.
* **Direction:** Confirm whether an identity provider exists. If it does, use the platform's JWT bearer handler. If API keys stay, cut the permissions down to what known callers need, for example a pricing client role and an operator role.

### 🟢 B5. The Technical Plan goes down to code-level detail

* **Lines:** 314–336, 365–375, 445–450; also 791–1559.
* **Problem:** The plan includes the full matching SQL and specific API calls. The implementation plan goes down to private records and a PowerShell one-liner.
* **Direction:** Describe what the query does (inputs, the three outcomes, the ordering) instead of its full text. Trim each file entry to its purpose and interface.

### 🟢 B6. Quotes copy fields from rows that cannot change

* **Lines:** 286–292.
* **Problem:** Quotes store foreign keys to the version and the rule, and also copy the version number, rule name, and multiplier from those append-only rows. Copying the policy values is justified, because policies can change.
* **Direction:** Keep the policy snapshot. Either drop the rule copies, or state that they deliberately guard against the db_owner bypass.

### 🟢 B7. Two pricing readers and a pass-through handler for one read

* **Lines:** 1026–1045, 1193–1200.
* **Problem:** `IPricingStatusReader` (Application) and a pass-through `GetActiveVersionHandler` sit alongside `IPricingRuleReader` (Domain). Both read the same active-version row.
* **Direction:** Merge them into one reader interface with two methods, or explain the split in one line.

## C. Inventive-catalyst only

### 🔴 C1. Publishing a full copy can silently revert another approved change

* **Promotion:** Raised as Major with a Blocker candidacy. The synthesizer promoted it because it is a correctness flaw that every database check misses.
* **Lines:** 147–154, 164–172, 189, 192, 256, 300–303, 365–375.
* **Problem:** Every version is a full copy, and the highest version number is the active one. The serializable lock stops two publishes from taking the same number, but nothing checks which version a script was written against. For example:
  1. Tickets A and B are both prepared from version 1.
  2. A publishes version 2.
  3. B then publishes version 3, a full copy of version 1 with B's change.
  4. A's change is gone, and every check passed.
* **Why it matters:**
  * It breaks G2 and the promise of the audit trail.
  * The history makes the silent revert look deliberate.
  * It is most likely during quick corrections such as CUJ 5.
* **Direction:** Add a required `@BaseVersionNumber` parameter to the procedure.
  * Reject the publish unless that number is still the latest version.
  * The template fills it in, so the reviewer sees it.
  * The cost: a stale script must be rebuilt.
  * This also underpins A2, C2, and C3.

### 🟡 C2. Full-copy scripts make the one changed row hard to see

* **Lines:** 118–120, 164–172, 548–571 (Alternatives F, G), 782–783, 1358–1368.
* **Problem:** The design treats storing a full copy and writing a full copy as the same choice. The cost that rejected delta versions applies only if the delta is worked out at read time. A script that restates hundreds of rules is exactly where 1.06 written as 1.6 (CUJ 5) gets past the reviewer.
* **Direction:**
  * Keep full copies in the tables, but let the procedure accept a delta against `@BaseVersionNumber`. The delta adds, changes, or removes rules, each identified by a stable rule key.
  * The procedure builds the full copy inside the transaction. Storage, tie prevention, and quote reproduction all stay as they are.
  * This also gives a one-line rollback: "republish version N".
  * The cost: a rule-key column and tested merge logic.

### 🟡 C3. A rule set goes live on commit with no preview

* **Lines:** 14–15, 192, 305–307, 365–375, 465, 581–590, 782–783, 1366–1367.
* **Problem:** The brief (Q16) asked for either a dry run or a stated non-goal, and the design has neither. The only way to see a script's effect is to make it live. A narrowed condition silently reprices every policy it no longer covers at the catch-all's 1.0, and this shows up only afterwards as Warning logs.
* **Direction:**
  * Add a `@DryRun` parameter that runs every check and then rolls back.
  * It returns the changes compared with the base version, and a count of existing policies whose winning rule or price would change. It highlights in particular any policy that would newly fall through to the catch-all.
  * The reviewer attaches this output to the ticket. This also delivers the "diff report" future improvement.
  * The cost: a longer procedure, and a scan of policies that grows with their number.

## Points for the author to settle

* **Where matching lives (B1).** The reviewers disagree: C# Domain or SQL.
* **Publish against a base version.** One change to the procedure covers C1, C2, C3, and A2: give it a base version and have it compare the new rules against that version.
* **Scope creep that zoom-out sees across the design:**
  * policy create/read/update with temporal history (A1);
  * five permissions (B4);
  * a metric that nothing exports (B3);
  * `READ_COMMITTED_SNAPSHOT` (A3).
