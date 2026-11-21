# Stage 7: Critic Review

* **Target:** `docs/designs/policy-pricing-rules.md`, FeatureRange (lines 1–790)
* **Context:** `concepts_brief.md`
* **Standards:** design and structural reviewers used feature-spec-standard. The style reviewer used its persona's own checks, because the persona names no standard file.

| Reviewer | Status | Raw findings |
| --- | --- | --- |
| egm-design | completed | 7 Major, 9 Nit |
| egm-structural | completed | 7 Major, 7 Nit |
| egm-style | completed | 6 Major, 20 Nit |

**After removing duplicates: 0 🔴 Blockers, 14 🟡 Majors, 27 🟢 Nits.** All three reviewers agree that the spec has every section the standard requires. They also agree it settles every item the brief marked "must be settled".

## 🟡 Major

### M1. Broken and unusable links in Additional Resources

* **Reviewers:** structural, style
* **Lines:** 771, 773–775
* **Problem:**
  * `./concepts_brief.md` resolves to `docs/designs/concepts_brief.md`, which does not exist.
  * The temporal-tables URL is inside backticks, so it is not a clickable link.
  * The ownership-chaining entry has no URL.
* **Why it matters:** The brief is the only record behind every "Rejected in review" in the Alternatives section.
* **Direction:** Link `../../concepts_brief.md`. Write every resource as `[text](url)`, and add the missing URL.

### M2. G5 claims history "cannot" be rewritten, but some logins can rewrite it

* **Reviewers:** style, design
* **Lines:** 75–77, 198, 356–357, 389–395, 476–477, 748–749
* **Problem:** G5 says SQL Server enforces it, yet the accepted risks admit db_owner and sysadmin can bypass it. Two more routes are never mentioned:
  * The `goldfish_migrator` login can ALTER the tables or the procedure.
  * The migration's `ALTER DATABASE ... READ_COMMITTED_SNAPSHOT` needs database-level ALTER rights, which are close to db_owner. It also needs `suppressTransaction`.
* **Why it matters:** An auditor who reads G5 on its own would believe something false. In practice the guarantee comes down to "migrations are code-reviewed".
* **Direction:**
  * Reword G5 to state its real limits.
  * Add the migrator and procedure-change routes to the accepted risks.
  * Consider moving the `ALTER DATABASE` step into the DBA's script.
  * Give a concrete reason for rejecting ledger tables (see M5).

### M3. The no-delay requirement conflicts with go-live that depends on when someone runs the script

* **Reviewers:** design, style, structural
* **Lines:** 53–55, 192, 298–307, 483–485, 592–604, 614
* **Problem:**
  * Line 54 says a delay "is noticeable"; line 614 says it "is unacceptable".
  * Timer caches are rejected on these grounds. Yet a change due at a set time goes live whenever the script happens to run, which could be minutes or hours early or late.
  * Brief question 12 asks what business event makes seconds matter. It is unanswered and missing from Open Questions.
  * Alternative J is rejected "while reconciling the draft with the concept brief". That records how the decision was made, not the trade-off. Its "can't test with a fake clock" con is weak, because the activation time can be passed in as a parameter.
* **Why it matters:** G2 and the rejection of caches both depend on this requirement. A change tied to a date is the most likely reason seconds would matter.
* **Direction:**
  * State the requirement once, with its business reason, or add question 12 to Open Questions.
  * If changes are tied to dates, bring back Alternative J: SQL Server's clock, an `EffectiveFromUtc` that never moves backwards, and a view of scheduled versions.
  * Otherwise, state that no change is tied to a time.
  * Either way, give J a real rejection reason.

### M4. Nothing checks the headline mistake before go-live, and dry run is neither in scope nor ruled out

* **Reviewers:** design, structural
* **Lines:** 44–48, 473–475, 548–559, 649–659, 782–785
* **Problem:**
  * The headline mistake (15 instead of 1.5) is accepted, and only a human guards against it.
  * Because each version is a full copy, a reviewer sees hundreds of rows rather than one change. Alternative F's advantage is given up and nothing replaces it.
  * The diff report and the multiplier-jump alert are deferred, with no cross-reference.
  * The brief asked for dry run to be either built or declared a non-goal (Q16). The spec does neither.
  * The brief's "script template with a validation step" was quietly replaced.
* **Why it matters:** The most likely real-world mistake has no control before it reaches customers.
* **Direction:** Choose one:
  * add `@DryRun` to the procedure, returning a rule-by-rule diff of old against new and then rolling back;
  * bring the multiplier-jump alert into scope;
  * at minimum, add the non-goal "No dry run or preview before publishing; see Future Improvements."

### M5. Several alternatives are rejected without weighing the trade-off

* **Reviewers:** structural, design, style
* **Lines:** 187–200, 602, 627–634, 661–668, 670–681, 706–715
* **Problem:**
  * **P (JWT):** the cons list only setup effort. The weaknesses of API keys are never weighed: shared secrets, no per-person identity, and manual rotation.
  * **Q (ledger tables):** ledger tables would close the db_owner risk, and the spec never says why the weaker option is enough. "Triggers block cleanup" conflicts with "nothing is purged".
  * **M (C#):** its only con hardly matters at a few hundred rules, and its rejection relies on integration tests the spec never commits to (M7).
  * **T:** three options are bundled into one entry, with a vague con.
  * **J:** see M3.
  * **H, L, G:** these cite decision numbers, but the decisions table gives only the outcome, not the reasoning.
  * **B–E:** these rest on a review whose link is broken (M1).
* **Why it matters:** The standard forbids rejecting an alternative only because someone said so, without a linked record of the reasoning.
* **Direction:**
  * For each rejection, add one sentence saying which con outweighs which pro. Examples: the SQL Server version available, whether an identity provider exists, and whether rule sets will stay small.
  * Split T into separate entries.
  * Link the review.

### M6. An alternative from the brief is missing

* **Reviewer:** structural
* **Lines:** the Alternatives section; brief line 161
* **Problem:** The brief rejected choosing the winner by most specific match, highest result, or newest rule. The spec only covers which way the priority runs (H).
* **Direction:** Add it as its own alternative, with ambiguity, unrepeatable results, and tie-breaking as the reasons.

### M7. Testing is missing from the scope sections, but a rejection depends on it

* **Reviewer:** structural
* **Lines:** 632–634; the brief's in-scope goals
* **Problem:** The brief includes a test project that runs against real SQL Server. The spec has no goal, non-goal, open question, or NFR about testing. Yet Alternative M's rejection relies on integration tests.
* **Direction:** Add a goal or an NFR item that names the test approach and the edge cases it must cover: boundaries, ties, no active version, and a publish that rolls back.

### M8. Risks are scattered across four places, and some have no mitigation

* **Reviewers:** structural, style
* **Lines:** 305–307, 394–395, 471–485, 754–765, 784–785
* **Problem:**
  * Risks appear in the Technical Plan's H3 section, in prose about active versions, in the permissions section, and in Rollout/Rollback.
  * Manual timing and privileged logins each appear twice.
  * "Last write wins" and "unreviewed base-price edits" have neither a mitigation nor a way to detect them.
* **Direction:**
  * Make an H2 "Risks & Mitigations" section, or merge risks into the NFR section.
  * Label each risk by type and give it either a mitigation or an "accepted because…" line.
  * Replace the scattered copies with pointers to that section.

### M9. The publisher login probably also needs permission on the table type

* **Reviewer:** design
* **Lines:** 361–363, 390
* **Problem:**
  * The ticket script declares a variable of type `pricing.PricingRuleInput`. In SQL Server that normally requires a separate permission on the type, and ownership chaining does not cover it.
  * The spec also assumes, without saying so, that one owner owns the schema and every object in it.
* **Why it matters:** The first production publish could fail with a permission error. Until it succeeds, every quote returns 503.
* **Direction:** Verify this. Add the grant on the type to the permission table and to `verify-permissions.sql`, and state who owns the schema.

### M10. Authorization depends on every controller remembering `[Authorize]`

* **Reviewer:** design
* **Lines:** 85–86, 734–736
* **Problem:** There is no global fallback policy. This contradicts G8 and the spec's own principle of "enforced, not habit".
* **Direction:**
  * Add a fallback policy that requires an authenticated user.
  * Mark `ProductsController` `[AllowAnonymous]`.
  * Add a test that fails if any endpoint outside an allow-list accepts anonymous requests.

### M11. Storage growth is not estimated, and nothing is ever purged

* **Reviewer:** design
* **Lines:** 114–115, 118–120, 280–296, 411–412, 745
* **Problem:**
  * At 10 quotes per second the Quotes table gains about 860,000 rows a day, around 300 million a year, or roughly 90 GB a year before indexes.
  * Quotes has no index beyond its primary key.
  * Quote creation has no idempotency key and no rate limit.
  * Retention is still an open question.
* **Direction:**
  * Add a yearly storage estimate.
  * Add indexes for audit lookups, on PolicyId and CreatedOnUtc.
  * Plan archiving, for example monthly partitions.
  * Consider a rate limit per API key.

### M12. The priority direction is worded inconsistently

* **Reviewer:** style
* **Lines:** 22–23, 190–191, 369; brief line 176
* **Problem:**
  * Line 22 says "the winning priority" without saying which one wins.
  * Line 369 says the catch-all's "priority is higher". With the lowest number winning, that reads backwards.
  * The brief says "highest priority wins".
* **Direction:** Always say "priority number" in comparisons. For example: "the matching rule with the lowest priority number sets the price", and "its priority number is greater than every other rule's".

### M13. The collation claim gives a reason that doesn't hold

* **Reviewers:** style, design
* **Lines:** 245, 271, 347–348
* **Problem:**
  * The phrase "checked on both sides" never names the sides.
  * Under the default case-insensitive collation, `CHECK (PolicyType IN ('Commercial','Personal'))` also accepts `commercial`.
  * What actually keeps matching consistent is that it runs only in SQL.
* **Direction:** Name the two sides (C# validation and the SQL CHECK) and give the real reason. Then either use a binary collation or a lookup table with a foreign key, or drop the claim.

### M14. "Should make impossible" hedges on the no-match case

* **Reviewer:** style
* **Lines:** 342–344
* **Problem:** A no-match can only happen if rule rows were written outside `pricing.PublishRuleSetVersion`.
* **Direction:** Say exactly that, so an operator who sees `Pricing.InvalidActiveVersion` knows where to look.

## 🟢 Nit

### Structure and formatting

* **N1** (structural, style), lines 769 and 777. Additional Resources and Future Improvements are H3s that sit under the NFR section. Make them H2s.
* **N2** (structural), lines 7, 725, 767, 789. Horizontal rules are used inconsistently. Use them between every H2 or not at all.
* **N3** (structural), lines 195–200. In the decisions table, rows 7–12 are labels rather than questions, under a column headed "Question". Rephrase them as questions, or rename the column "Topic".
* **N4** (structural), lines 249 and 291. A few data-model rows cover several columns, or say only "as on Policies". Give each column its own row with its type.
* **N5** (structural), lines 401–406. The error cells in the API table are uneven, and 401/403 appear only in the prose. Write every cell as "status + code".
* **N6** (structural). Add a user journey for a caller without a valid key, and one for a quote requested before any version is published.

### Design accuracy

* **N7** (design), lines 365, 370–371. Two concurrent publishes that each read MAX(VersionNumber) under SERIALIZABLE usually deadlock rather than wait. Use `UPDLOCK, HOLDLOCK` or an application lock, and describe the unique index as the real guard.
* **N8** (design), lines 71–74 vs 280–296. G4 says no other table is needed, but explaining a quote still needs the rule's conditions from PricingRules. Reword G4, or copy the conditions onto the quote.
* **N9** (design), lines 286–288. Use a composite foreign key on (VersionId, RuleId), so a quote's rule must belong to its version.
* **N10** (design), lines 248–249, 259, 296. Timestamps come from two clocks: the application's TimeProvider and SQL Server's. Use SQL Server's clock everywhere, or document that the two must not be compared.
* **N11** (design), lines 465–467, 588–590. Warnings for quotes priced by the catch-all may become noise. Either give every segment an explicit rule, or log at Information and rely on the metric.
* **N12** (design), lines 436–450. Missing key-handling details:
  * the minimum randomness of a key, since unsalted SHA-256 is safe only for long random keys;
  * whether HTTPS is required;
  * whether rotating a key needs a restart.
* **N13** (design), lines 762–764. Make `Down` throw when data exists or when the target is not a development database, rather than relying on a warning.
* **N14** (design, style), lines 195, 645–647, 737–745.
  * Say who confirmed banker's rounding with finance, or make it an open question.
  * Use one term for it: "banker's rounding" or `MidpointRounding.ToEven`, not both.
  * State the expected behavior while SQL Server is down, or list it as an accepted limit.
* **N15** (style), lines 290, 294, 321, 328. The same flag is called `WasCatchAll` in some places and `IsCatchAll` in others. Pick one name or explain the mapping.

### Wording and claims (style)

* **N16**, line 57. "None of this today": "this" has no clear antecedent.
* **N17**, lines 36–37. This repeats line 12 in the passive voice.
* **N18**, line 39. "Adds days of delay" has no source. Write "adds a release cycle to each change".
* **N19**, line 47. "Clearly invalid" is vague. Point to G6's list.
* **N20**, lines 80, 374. "A minimum above a maximum" is wrong, because the constraint is Min < Max. Write "not below".
* **N21**, lines 83–84. "Enough detail to trace it" can't be measured. Point to the list of logged fields.
* **N22**, lines 351–352. Replace "so it stays correct" with the specific fact that holds.
* **N23**, lines 517, 523, 525–526. "Mature" is subjective, and "far more than … needs" appears twice.
* **N24**, lines 532, 535. "Cannot validate" overstates it, because ISJSON and JSON_VALUE work in CHECK constraints.
* **N25**, lines 543–544, 742. The load claims rest on an open question. Mark them as estimates.
* **N26**, lines 674, 688, 711–712. "Can't" and "cannot" are mixed. "You" appears only here, and "actually" is filler.
* **N27**, lines 720, 755. "A large separate build" and "safe and clearly reported" are vague. Make them concrete.

## Priority

The design reviewer wants three Majors settled before implementation: M3 (the timing requirement), M4 (a check before go-live, or a non-goal), and M9 (permission on the table type).

The rest fall into two groups:

* **Fixed by editing text alone:** M1, M5, M6, M7, M8, M12, M13, M14.
* **Need small design additions:** M2, M10, M11.
