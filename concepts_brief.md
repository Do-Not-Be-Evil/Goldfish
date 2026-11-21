# Policy Pricing Rules for the Goldfish API

* **Author:** Do-Not-Be-Evil
* **Date:** 2026-11-21

## Overview

Insurance policy prices in the Goldfish API should come from pricing rules that business analysts change about once a month. A change must not wait for a code release, and a delay of a few seconds after approval is not acceptable. Every change goes through the existing change-ticket review, and every price must be explainable later.

The feature adds three things to the existing .NET 10 Clean Architecture solution:

1. **Policy tables.** Each policy stores its type (for example Commercial), its coverage limit, and its own base price.
2. **Pricing rules stored in SQL Server.** Each rule has conditions on policy fields, a multiplier, and an explicit priority. When several rules match, the one with the highest priority wins. Example: if the policy type is Commercial and the coverage limit is over $1,000,000, the price is 1.5 times the base price.
3. **A quote endpoint.** It loads the policy, reads the current rules from the database on every request, picks the winning rule, computes the price, and saves a quote record showing exactly how the price was produced.

Approved rule changes reach the database through SQL scripts attached to change tickets. This project builds no editing screen and no approval workflow.

## Analysis

All seven review lenses found the concept workable. They also agreed that the main risk is undefined terms, not technology. They recommend a design simpler than a "rules engine": a versioned table of typed rules plus a small evaluation function written in C#. No third-party engine is needed.

### Core contradictions and how to resolve them

**1. Changes must be visible at once, but quotes must be fast.**

The usual fix for speed is to cache rules in memory, and any time-based cache breaks the no-delay requirement. The resolution is to not cache. Each quote runs one indexed query for the current rule set. With monthly changes and normal traffic this costs very little.

If load ever demands a cache, key it by version: each request reads a single "current version id" row and reloads the rules only when that id changes.

There is one hard limit. A change cannot apply before the ticket script commits it. "Immediately after approval" therefore means "on the first request after the script runs". Any gap between approval and running the script belongs to the ticket process.

**2. Analysts change rules monthly, but pricing must stay safe.**

Split structure from values:

* **Structure** is the fixed set of fields a rule can test (policy type, coverage limit range). It changes only through a code release.
* **Values** are rows (which type, which range, which multiplier, which priority). They change through tickets.

Database constraints reject bad values when the script runs: the multiplier must be above zero, the minimum must not exceed the maximum, and each priority must be unique. A typo then fails the script in front of the reviewer instead of mispricing live quotes.

**3. Rules must never be edited, but mistakes must be fixable.**

Correct a mistake by publishing a new version that replaces the bad one. Block UPDATE and DELETE on rule tables in SQL Server itself, through permissions, triggers, or ledger tables. Then the only way to change a rule is the auditable way. Today "never edited in place" is only a convention, and one manual UPDATE would silently break the audit trail.

**4. Versioning single rules versus reproducing a quote.**

Which rule wins depends on every rule active at that moment, not only the winner. So version the whole rule set:

* One version header row owns all the rules in that set.
* The header and its rules are inserted in a single transaction, so no request can see a half-loaded set.
* Each quote stores the version id and the id of the rule that won.

**5. The base price can change, but the price history must hold.**

Each quote saves the values it was computed from: policy type, coverage limit, base price, multiplier, and final price. It can then be explained later without depending on data that may have changed since. If base-price edits must also be audited, the Policy table can become a SQL Server temporal table, which keeps every previous row automatically.

**6. Effective dates versus "applies on the next request".**

A version dated in the future does not apply on the next request. Either forbid future dates, or define "immediate" as "once committed and its effective date has passed". The second option lets a ticket run early and take effect exactly on schedule.

### Ideal Final Result

The rule tables reject bad data on their own, and one clock decides which version is active. A quote is one indexed read of the current version plus a pure function that picks the winner. The quote record stores the winning rule and the inputs. There is no engine, no cache, and no invalidation messaging. Changes are inserts that the database refuses to edit, so the audit trail is a side effect of the design rather than a separate system.

### Other findings

* **"Version" is not yet defined.** It could mean one rule or the whole set, and it is unclear whether one ticket can change several rules. Every lens rated this the top blocker.
* **Priority direction and ties.** It must be stated whether 1 or the largest number wins. A unique constraint on (version, priority) makes a tie impossible. Without it, the winner would depend on the order SQL Server happens to return rows, which is not guaranteed.
* **No rule matches.** Pick one behavior: every version must contain a lowest-priority catch-all rule with multiplier 1.0, or the endpoint returns an explicit error. Never fall back to the base price silently.
* **Boundaries.** "Over $1,000,000" means a policy at exactly $1,000,000 does not match. Each rule should record whether its limits are inclusive or exclusive, and tests should check the exact boundary value.
* **One authoritative clock.** If the ticket script stamps database time and the app compares against a web server clock that lags, a new version stays invisible for the length of that gap. Choose database time (`SYSUTCDATETIME()`) or application time (`TimeProvider`) and use it everywhere. Also decide whether the version is chosen by the time of the request or by the policy's start date.
* **Rounding and money.**
  * Store the multiplier as `decimal(9,4)`, and the base price and coverage limit as `decimal(18,2)`.
  * Define the rounding mode for the final price. .NET rounds to even by default, which finance may not expect.
  * State single currency as an assumption.
* **Case-sensitive text.** SQL Server's default collation ignores case and C# comparisons do not, so "Commercial" and "commercial" could price differently depending on where matching runs. Use a closed list of policy types.
* **Rule data must not go through EF migrations.** Migrations own the table structure, and ticket scripts own the rule rows. Seeding rules with `HasData` would turn every monthly change into a code release, and later migrations would conflict with rows that tickets inserted.
* **No authentication exists.** `Program.cs` calls `UseAuthorization()` but registers no authentication scheme. A quote endpoint that exposes pricing and writes audit records would be open to anyone.
* **No tests, logging, or telemetry exist.** There is no test project.
  * Testing the transaction, constraint, and collation behavior needs a real SQL Server (LocalDB or Testcontainers). EF's in-memory provider ignores all three.
  * To answer "why did this policy get this price?", log each pricing decision with the policy, quote, version, rule, and multiplier, and add a way to see which version the app considers active.
* **Error types.** `Error` has only a code and a description, so the controller would have to switch on code strings to choose between 404, 409, and 422. Add an error category, or a single place that maps errors to responses.
* **Transactions and retries.** `EnableRetryOnFailure()` throws on a manually started transaction. The quote handler does not need one: read the rules without tracking, add the quote, and save once.
* **Terms are unclear.** In insurance a quote normally comes before a policy, but here the policy already exists. Define Policy and Quote in one sentence each, and say whether one policy can have many quotes.
* **No dry run.** Without an editing screen, a mistake in a valid-looking script goes live on the next request. A reviewed script template, plus a validation query or a preview against sample policies, lowers that risk. Otherwise, list it as an accepted risk.

### Unresolved reviewer disagreements

1. **How to store conditions.**
   * Typed columns on each rule row (`PolicyType`, `MinCoverageLimitExclusive`, `MaxCoverageLimitInclusive`): zoom-out, inventive catalyst, codebase details.
   * A child table of conditions limited to fixed field and operator lists: implementation readiness, design, structural.
   * What decides it: how many new condition fields will realistically appear in the next year.
2. **Where matching runs.**
   * In SQL (filter, order by priority, take the top row): one round trip and one clock.
   * In a pure C# domain function: fully unit-testable and no collation differences.
3. **Which clock is authoritative:** the database or the application.
4. **Future-dated versions:** forbid them, or allow scheduled activation.

### Proposed data model

* **Policy:** Id, PolicyType, CoverageLimit `decimal(18,2)`, BasePrice `decimal(18,2)`, CreatedOnUtc.
* **PricingRuleSetVersion:** Id, VersionNumber (unique), EffectiveFromUtc, ChangeTicketRef, ApprovedBy, CreatedOnUtc. Insert only.
* **PricingRule:** Id, VersionId, Priority (unique per version), Multiplier `decimal(9,4)` with a CHECK constraint above zero, Name, and the condition columns or child condition rows. Insert only.
* **Quote:** Id, PolicyId, VersionId, MatchedRuleId, the evaluated PolicyType, CoverageLimit and BasePrice, Multiplier, FinalPrice, CreatedOnUtc. Insert only.

A version has no end date: it ends when the next one starts, so gaps and overlaps cannot occur.

### Open questions

1. Is the version the whole rule set or each rule?
2. Can versions be future-dated?
3. Which clock decides the active version? Is the version chosen by request time or by the policy's start date?
4. Does the larger or the smaller priority number win?
5. Is no match an error, or must every version contain a catch-all rule?
6. Which fields and operators can conditions use in version 1? Is policy type a closed list?
7. What exactly is a Policy? How are policies created, and how do their base prices change?
8. Does the audit trail cover base-price edits, or only rule changes and quotes? What is the retention period?
9. What rounding mode and currency apply? Are multipliers below 1.0 (discounts) allowed?
10. Is every quote saved? Do repeated requests need an idempotency key?
11. Who writes and runs the ticket scripts? Can the database logins be limited to INSERT on rule tables? Who runs migrations in production?
12. Which business event makes a few seconds of delay unacceptable?
13. What are the expected peak quotes per second and rules per version?
14. Who calls the quote endpoint, and how do they authenticate?
15. Is a new test project using LocalDB or Testcontainers approved?
16. Is a preview or dry run of a new version needed, or is it a stated non-goal?

## Goals vs. Non-Goals

* **In-Scope Goals:**
  * Policy table holding type, coverage limit, and base price, following the existing entity, factory, and `Result` conventions.
  * Pricing rule sets versioned as a whole and effective-dated, each with a change-ticket reference and published in one transaction.
  * Immutability enforced in SQL Server. Corrections only by publishing a new version.
  * Deterministic evaluation:
    * a fixed list of condition fields, combined with AND only;
    * unique priority per version, with a defined direction;
    * defined behavior for no match, no active version, and boundary values;
    * defined rounding.
  * `POST /api/policies/{policyId}/quotes`, returning 201, and `GET /api/quotes/{id}`. Rules are read fresh on every request. Errors are returned as ProblemDetails.
  * A saved quote record that explains itself: version, matched rule, inputs, multiplier, and final price.
  * A reviewed script template for publishing rule versions, with a validation step.
  * Structured logging of each pricing decision, and a way to see the active version.
  * A test project that runs the edge cases against a real SQL Server.
  * Authentication on the quote endpoint.
* **Out-of-Scope Non-Goals:**
  * A rule-editing screen or an in-app approval workflow (stated by the author).
  * A general expression language, scripted rules, or a third-party rules engine.
  * OR or nested conditions, stacked multipliers, fees, or price caps.
  * Rule caching, cache invalidation messaging, or push notifications.
  * Looking up the base price from a rate table (assumed: the base price is stored on each policy).
  * Multiple currencies.
  * Re-pricing existing quotes when rules change.
  * Rule data delivered through EF migrations or `HasData`.

## Alternatives Weighed

* **Plain C# code for each rule.** Rejected by author: analysts change rules monthly, and changes must not wait for a code release. It remains the simplest baseline if the ticket process could ever ship releases fast enough.
* **A fixed base price of $100,000 for all policies.** Rejected by author: the base price varies per policy.
* **Base price supplied by the caller in the request.** Rejected by author: the base price comes from data stored in this system.
* **Base price stored in an external or existing system.** Rejected by author: new tables in the Goldfish database will hold policies and base prices.
* **Building an editing UI and approval workflow.** Rejected by author: review already happens in the existing change-ticket system.
* **Rule winner chosen by most specific match, highest result, or newest rule.** Rejected by author in favor of an explicit priority number.
* **Microsoft RulesEngine.** Rejected in Goldfish review:
  * rules are JSON workflows containing lambda strings evaluated at runtime, which is code stored as data, and errors surface late;
  * it has no built-in "highest priority wins", so results must be sorted afterward;
  * it keeps compiled workflows in memory, which conflicts with the no-delay requirement unless rebuilt on every request;
  * its expression library, System.Linq.Dynamic.Core, had a remote code execution vulnerability (CVE-2023-32571, fixed in 1.3.0).
* **NRules.** Rejected in Goldfish review: rules are compiled C# classes, so changes need a release. Loading rules from the database means building rule models at runtime. Its forward-chaining design does far more than picking one winning rule needs.
* **A JSON column of conditions on each rule.** Rejected in Goldfish review: the database cannot validate its contents, and it is harder to query for audit.
* **In-memory cache with push invalidation (SqlDependency or Service Broker).** Rejected in Goldfish review: the complexity is not justified. A per-request read, or a cache keyed by version, meets the requirement.

## Key Decisions & Staging for Next Steps

**Decided:**

* Rules live in SQL Server and are read through EF Core 10.
* Each rule has conditions on policy fields, a multiplier on the policy's base price, and an explicit priority. The highest priority wins.
* Rules are never edited in place. Changes arrive as approved change-ticket scripts.
* A change applies on the first request after it is committed, with no cache delay.
* Each quote records which rule version priced it.
* Policies and their base prices are stored in new tables in the Goldfish database.

**Recommended by all reviewers, pending author confirmation:**

* Version the whole rule set, not individual rules.
* Read rules fresh on every quote, with no cache.
* Enforce immutability with SQL Server permissions or triggers.
* Save a full snapshot of inputs and outputs on each quote.
* Use no third-party rules engine.

**Must be settled before `/egm-draft`:**

* the meaning of "version";
* priority direction;
* no-match behavior;
* future-dated versions;
* which clock decides the active version;
* condition storage (typed columns or condition rows);
* where matching runs (SQL or C#);
* rounding mode;
* the endpoint's authentication model.
