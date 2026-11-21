# Stage 6: Comprehension Test

* **Target:** `docs/designs/policy-pricing-rules.md`. FeatureRange is lines 1–790 and DetailedRange is lines 791–1589.
* **Reviewer:** egm-comprehension. It read the document alone and applied no standard.

**What this proposes**

Insurance policy pricing moves out of C# code and into rules stored in SQL Server, so the business analysts' roughly monthly changes need no build or deployment.

* **Policies.** A policy stores its type (Commercial or Personal), its coverage limit, and its base price.
* **Rules.** A rule has up to three optional conditions: the policy type, a minimum coverage that is exclusive, and a maximum coverage that is inclusive. It also has a multiplier and a priority.
* **Versions.** Rules are published as complete "versions" that are never changed after publishing. A stored procedure checks each version before it is saved. The newest saved version takes effect at once. Each version must contain exactly one catch-all rule, and it must have the highest priority number.
* **Quotes.** A quote endpoint runs one SQL query to find the matching rule with the lowest priority number in the newest version. It multiplies the base price by that rule's multiplier and rounds to 2 decimal places, sending halves to the nearest even digit. It then saves a quote that keeps every input, so the price can be explained later.
* **Protection.** API keys with named permissions protect the endpoints. Database permissions, not habit, stop published rules from being edited.
* **History.** Policies are stored in a temporal table, so edits to base prices leave a history.

**How the existing system works, per this document**

Goldfish today is a layered .NET solution with four projects: Api, Application, Domain, and Infrastructure. It uses EF Core 10 on SQL Server.

* Its only data is a sample `Products` table, served by an anonymous `ProductsController`.
* It has no policy tables, no pricing logic, no tests, no logging, and no authentication.
* The document mentions several existing patterns without describing them: a `Result`/`Error` type, an `Entity` base class, `Product.Create`, a UTC converter in `ProductConfiguration`, explicit DI registration, and, by implication, `EnableRetryOnFailure`.
* Controllers currently switch on error-code strings to choose HTTP status codes.
* Rule changes go through an existing change-ticket review process outside the system.

The reviewer could paraphrase every section, so there are no Blockers. Core terms are defined before they are used: version, catch-all, priority direction, active version, and quote snapshot.

## Findings

### 🟡 Major: The document never says who writes or runs a ticket script

* **Lines:** 12–15, 303–307, 390, 753, 1358–1368.
* **Problem:** Ticket scripts are the main path for the whole feature (G1). Yet the document never says who writes the script, who runs it, which tool they use (sqlcmd, SSMS, or a pipeline), or how they get the `goldfish_pricing_publisher` credentials. Line 306 says "whoever runs the script". Alternative U suggests analysts write SQL. Line 1398 says analysts "work outside the code".
* **Why it matters:** A reader can't picture the main workflow from start to finish. The accepted risk about manual timing depends on who runs the script. Nobody can judge whether an EXECUTE-only login fits how scripts are actually run.
* **Direction:** Add a short "publishing workflow" paragraph that names each role: who drafts the script, who approves it, and who runs it, with which login and tool.

### 🟡 Major: It is unclear which checks the procedure makes and what errors they raise

* **Lines:** 158–162, 365–375, 1146–1149, 1576.
* **Problem:** The document contradicts itself on errors:
  * CUJ 4 promises "an error naming the problem" for a duplicate priority and for a multiplier of 0.
  * Lines 372–374 say CHECK constraints and unique indexes catch those cases. Those raise generic SQL Server errors.
  * Line 1148 specifies `THROW 50001`–`50005` with "a clear message for each check". But the five listed steps contain only two real checks: an empty list and the catch-all rule.
  * Line 1576 requires a test for "every check in the procedure".
* **Why it matters:** A reader can't list the checks, and can't tell whether CUJ 4's promise holds. The runbook needs exactly this list.
* **Direction:** List each check with its error number and message. Then either add explicit pre-checks for multiplier, priority, minimum/maximum, and duplicates, or soften CUJ 4 to say that constraint violations show up as SQL Server's own errors.

### 🟢 Nit: "Started" wording is left over from future dating

* **Lines:** 877, 1038.
* **Problem:** "When no version has started" implies that versions have start times, which decision 4 removed.
* **Direction:** Use "has been published".

### 🟢 Nit: "Priority is higher" is ambiguous

* **Line:** 369.
* **Problem:** The phrase can be read backwards, because the lowest priority number wins. This is the validation spec, so the wording matters.
* **Direction:** Write "has a larger priority number than every other rule".

### 🟢 Nit: The document cites a test-database decision that doesn't exist

* **Line:** 1456.
* **Problem:** "Decision on test database (LocalDB)" refers to a decision that isn't in the decisions table.
* **Direction:** Add it as decision 13 with its reason, or reword the line.

### 🟢 Nit: "Review" and "the concept brief" point outside the document

* **Lines:** 511, 524, 535, 543, 602–603, 771–772.
* **Problem:**
  * Several rejections say "Rejected in review" without saying which review.
  * Alternative J says it was rejected "while reconciling the draft with the concept brief", but the brief isn't part of this document.
  * "Goldfish review" collides with Goldfish, the name of the solution.
* **Direction:** Explain the review in one line, and say what the brief contributed to decision 4.

### 🟢 Nit: Patterns from the existing code are assumed knowledge

* **Lines:** 834, 845, 1017, 1078.
* **Problem:** `Entity`, `Product.Create`, the UTC converter, and the conflict between `EnableRetryOnFailure` and explicit transactions are mentioned but never described.
* **Direction:** Add one clause for each.

### 🟢 Nit: The document never identifies the callers

* **Lines:** 26, 132, 206.
* **Problem:** "Caller" is never defined: it could be internal systems, a front end, or people.
* **Why it matters:** The callers are part of the reason for choosing API keys and for how the permissions are split.
* **Direction:** Add one sentence to the Overview naming the expected callers.

### 🟢 Nit: "Policy" has two meanings

* **Lines:** 448–449, 1228–1229.
* **Problem:** The word means both an insurance policy and an ASP.NET authorization policy.
* **Direction:** Write "authorization policy" whenever the ASP.NET sense is meant.

### 🟢 Nit: The configuration shape is inconsistent

* **Lines:** 436 and 1213–1217, 1313, 1464.
* **Problem:** The Technical Plan puts the keys directly under `ApiKeys`, while the Detailed Plan uses `ApiKeys:Keys`.
* **Direction:** Use `ApiKeys:Keys` in both places.

### 🟢 Nit: A reference has no link

* **Line:** 775.
* **Problem:** The reference on ownership chaining has no link, and the no-edit guarantee in G5 depends on ownership chaining.
* **Direction:** Add the Microsoft Learn URL.
