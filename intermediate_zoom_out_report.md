# Stage 8: Intermediate Zoom Out Review

* **Target:** `docs/designs/policy-pricing-rules.md`, lines 1–1589. The file has not changed since Stage 5.
* **Context:**
  * `concepts_brief.md`
  * `zoom_out_report.md`
  * `comprehension_report.md`
  * `critic_report.md`
* **Reviewer:** egm-zoom-out, which completed.
* **Findings:** 🔴 1 Blocker, 🟡 9 Major, 🟢 3 Nit.

**Fact-checks against the document:**

* The unique index on `VersionNumber` exists (lines 256, 261, 1114).
* The detailed plan already includes an index on `Quotes.PolicyId` (line 1090). Stage 7 M11 is wrong on this point, except that the data-model table at lines 280–296 leaves the index out.

**Verdict:** The core design is still sound. The risk now is that applying every fix from Stages 5–7 would make it larger. Most of the problems they found go away by *removing* something.

## 🔴 Blocker

### Z1. Fix the silent revert with the existing unique index, not with more locking

* **Lines:** 256, 365–375, 1144–1149.
* **Covers:** Stage 5 C1 and Stage 7 N7.
* **Problem:** The proposed fixes would stack a base-version check on top of a locking scheme: `UPDLOCK, HOLDLOCK`, or an application lock.
* **Direction:**
  * Make `@BaseVersionNumber` a required parameter.
  * Number the new version `@BaseVersionNumber + 1`.
  * Any script built on an old version, and either of two simultaneous publishes, then collides on the existing unique index.
  * Add one pre-check that the base is still the latest version, so the error says "rebuild the script".
  * Remove the SERIALIZABLE wording and the MAX read.
  * Make the template fill in the base version number.

## 🟡 Major

### Z2. Use one dry run that returns a diff, and drop the other three proposals

* **Lines:** 44–48, 196, 473–475, 649–659, 782–785, 1358–1368.
* **Covers:** Stage 5 A2, C2 and C3; Stage 7 M4.
* **Problem:** Each of the alternatives costs more than it gives:
  * **Ratio thresholds (A2)** need an owner, and people end up approving the override by habit.
  * **Delta publishing (C2)** adds a second way to publish and brings back Alternative G.
  * **The full policy scan (C3)** grows with the number of policies.
  * **An alert** only fires after customers have already seen the wrong price.
* **Direction:**
  * Add `@DryRun bit`. It runs the same checks, then returns each rule's old multiplier, new multiplier and their ratio compared with the base version, then rolls back.
  * Match rules by `Name`, using one more unique index on (VersionId, Name). No new column is needed.
  * The dry-run output gets attached to the change ticket.
  * Drop C2, A2's thresholds, C3's policy scan and the alert. The scan can become a future improvement.
  * This also covers the brief's "diff report" and "validation step".

### Z3. Remove policy updates instead of adding controls to them

* **Lines:** 174–181, 199, 240–249, 389, 403, 478–482, 683–693, 786, 967–976, 1066–1080, 1452–1455, 1531.
* **Covers:** Stage 5 A1 and Stage 7 M8.
* **Problem:** "Last write wins" and "unreviewed base-price edits" exist only because `PUT` exists. Brief Q7 is still unanswered.
* **Direction:**
  * Adopt Alternative R: policies can be created and read, but not updated.
  * Remove:
    * CUJ 6;
    * the temporal table and its history table;
    * the grant on the history table;
    * the toggle that turns versioning off during test resets;
    * the two accepted risks.
  * Move "how are base prices corrected?" into Open Questions.
  * G4 is unaffected, because quotes already store a snapshot of the base price.

### Z4. Remove `READ_COMMITTED_SNAPSHOT` instead of moving it

* **Lines:** 377–379, 743–744, 748–749, 756–758, 1150–1153.
* **Covers:** Stage 5 A3 and Stage 7 M2.
* **Problem:** Rows are only ever inserted, so a committed version is always complete. Under the default isolation level, a quote can't see half of a version. At worst it waits a few milliseconds, about once a month.
* **Direction:**
  * Delete the setting.
  * This also removes the `ROLLBACK IMMEDIATE` window, the `suppressTransaction` workaround, the step that `Down` cannot undo, and the change to how Products are read.
  * Replace it all with one sentence explaining why the default isolation level is enough.
  * What remains of M2 is wording only: state G5's real limits, and list the migrator login as an accepted risk.

### Z5. Do the matching in C#, because the reasons for SQL no longer hold

* **Lines:** 194, 309–352, 627–634, 865–880, 1177–1200, 1441–1442, 1571–1572.
* **Covers:** Stage 5 B1, B7 and A4; Stage 7 M5 (Alternative M), M7 and M13.
* **Problem:** SQL matching was chosen for reasons of clock and collation. Decision 4 removed the clock. The closed enum already makes collation irrelevant to matching.
* **Direction:**
  * Change decision 6. Load the latest version and its rules with `AsNoTracking`, then pick the winner in a pure Domain function.
  * The active-version endpoint reuses the same read. That removes the separate status reader and its handler (files 23–25 and 39).
  * Matching edge cases become unit tests.
  * For M13, set `Latin1_General_BIN2` collation on both `PolicyType` columns.
  * LocalDB is then needed only for the procedure and permission tests. State the Windows CI trade-off in one line.
  * M7 becomes one NFR line about how the tests are split.

### Z6. Decide between JWT and API keys first, then keep authentication small

* **Lines:** 197, 434–450, 661–668, 734–736, 1211–1245, 1310–1316, 1464–1469.
* **Covers:** Stage 5 B4; Stage 7 M5 (Alternative P), M10 and N12.
* **Direction:**
  * Add "Is there an identity provider?" to Open Questions. If there is one, use the standard JWT handler. That replaces files 41–43 and answers N12.
  * If API keys stay:
    * cut the permissions down to two roles: pricing client and operator;
    * add a default authorization policy that denies anonymous access (M10), and mark Products `[AllowAnonymous]`;
    * skip the test that scans every endpoint, because the 401 tests already cover it;
    * answer N12 in three sentences: keys are 32 random bytes, HTTPS is required, and rotating a key needs a restart.

### Z7. Estimate storage, but don't partition or rate-limit

* **Lines:** 114–120, 280–296, 411–412, 745, 1090.
* **Covers:** Stage 7 M11.
* **Direction:**
  * Add the `PolicyId` index to the data-model table.
  * Add a one-line yearly storage estimate, based on the assumed load.
  * Leave retention as an open question.
  * Don't add partitions, archiving or a rate limit yet.

### Z8. Settle the logging and metric question one way

* **Lines:** 465–467, 589–590, 931–937, 991–999, 1012–1014, 1052–1053.
* **Covers:** Stage 5 B2 and B3; Stage 7 N11.
* **Problem:** N11 says to log at Information and "rely on the metric". B3 says to remove the metric. Doing both leaves nothing to rely on.
* **Direction:**
  * Log catch-all quotes at Information.
  * Remove `QuoteMetrics`, its two package references and `AddMetrics`.
  * Monitor with the stored `WasCatchAll` and version columns, plus one query in the runbook.

### Z9. Name constraints clearly instead of repeating them as procedure checks

* **Lines:** 156–162, 365–375, 1146–1149, 1576.
* **Covers:** the Stage 6 Major.
* **Direction:**
  * Give each constraint a descriptive name, for example `CK_PricingRules_Multiplier_Positive`, so the SQL Server error names the problem.
  * Use `THROW` in the procedure only for the three checks constraints can't express: an empty rule set, the catch-all rule, and a stale base version (Z1). Document those three errors.
  * Don't adopt ledger tables (Stage 7 M5, Alternative Q). Give one sentence on why: the SQL Server version they need, and the purge limits they impose.

### Z10. Ask the timing question instead of rebuilding Alternative J now

* **Lines:** 53–55, 305–307, 483–485, 592–615.
* **Covers:** Stage 7 M3.
* **Direction:**
  * Add brief Q12 to Open Questions.
  * Choose one word, "noticeable" or "unacceptable", and use it throughout.
  * Give Alternative J a real reason for rejection.
  * Note that a change due at a set time can be handled by scheduling the ticket script itself, for example with a SQL Agent job, with no schema change.

## 🟢 Nit

* **Z11.** Two small fixes from Stages 6 and 7 are worth keeping:
  * a short publishing-workflow paragraph covering roles, login and tool (Stage 6);
  * `GRANT EXECUTE ON TYPE::pricing.PricingRuleInput` in both the permission table and `verify-permissions.sql` (Stage 7 M9).
* **Z12.** Where a Stage 7 nit offers a text-only fix, prefer it:
  * N8: reword G4 to "needs only the immutable rule rows".
  * N9: keep the composite foreign key.
  * N10: document that the timestamps are informational only.
  * N13: a throwing `Down` is optional.
  * N6: skip the extra CUJs, or keep them to two lines each.
* **Z13.** Explain each assumed code pattern in one clause only. Together with Stage 5 B5, the detailed plan should get shorter, not longer.

## Recommended fix set (the simplest combination)

1. **Publishing.**
   * Number the new version `@BaseVersionNumber + 1`, so the unique index catches stale and simultaneous publishes.
   * Add `@DryRun`, returning a per-rule diff matched on a unique `Name`.
   * Drop SERIALIZABLE.
   * Don't add delta publishing, thresholds, the policy scan or the alert.
   * Covers Stage 5 C1, C2, C3 and A2; Stage 7 M4 and N7.
2. **Isolation.** Remove `READ_COMMITTED_SNAPSHOT`. (Stage 5 A3; Stage 7 M2)
3. **Matching.**
   * A pure C# Domain function over one plain read, which the active-version endpoint also uses.
   * Binary collation on `PolicyType`.
   * LocalDB only for the procedure and permission tests.
   * Covers Stage 5 B1, B7 and A4; Stage 7 M5 (M), M7 and M13.
4. **Policies.** Create and read only. No temporal table and no CUJ 6. Base-price correction goes to Open Questions. (Stage 5 A1; Stage 7 M8)
5. **Authentication.** Ask whether an identity provider exists, and use JWT if so. Otherwise, API keys with two roles, a default policy that denies anonymous access, and three sentences on key handling. (Stage 5 B4; Stage 7 M5 (P), M10 and N12)
6. **Observability.** Log catch-all quotes at Information, remove the metric, and add a runbook query. (Stage 5 B2 and B3; Stage 7 N11)
7. **Errors.** Named constraints, plus only three procedure errors. (Stage 6)
8. **Open Questions to add:**
   * the timing driver (brief Q12);
   * whether an identity provider exists;
   * how base prices get corrected.

   Don't build Alternative J, partitions or a rate limit. (Stage 7 M3 and M11)
9. **Text-only fixes:**
   * the Stage 6 workflow paragraph;
   * the type grant (M9);
   * Stage 7 M1, M2 (wording), M5, M6, M12 and M14;
   * the nits.

**Net effect:** about 12 planned files and 2 accepted risks are removed. The only new mechanism is `@DryRun`.
