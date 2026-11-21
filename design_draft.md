# Policy Pricing Rules

*   **Author:** Do-Not-Be-Evil
*   **Status:** Draft
*   **Date:** 2026-11-21

--------------------------------------------------------------------------------

## Overview

The Goldfish API will price insurance policies using rules stored in SQL
Server instead of rules written in C#. Business analysts change pricing about
once a month. Each change goes through the existing change-ticket review, then
arrives as a SQL script. It takes effect on the first quote request after the
script commits, and needs no code release or restart.

The feature has three parts:

1.  **Policies.** A new table where each policy records its type (for example
    Commercial), its coverage limit, and its own base price.
2.  **Pricing rules.** Each rule states conditions on policy fields, a
    multiplier, and a priority number. When more than one rule matches a policy,
    the rule with the winning priority sets the price. Rules are grouped into
    versions. A version is never changed after it is published; a correction is
    a new version.
3.  **Quotes.** A new endpoint takes a policy, finds the winning rule in the
    current version, multiplies the base price, and saves a quote record. The
    record holds every value the price came from, so the price can be explained
    later even after the rules or the policy change.

Example rule: if the policy type is Commercial and the coverage limit is over
$1,000,000, the price is 1.5 times the base price.

## The Problem

Pricing changes are driven by business analysts on a monthly cycle. If pricing
lives in C# code, every monthly change needs a developer, a build, and a
deployment. That puts engineering in the path of a business decision and adds
days of delay to each change.

Moving the rules into the database removes that dependency. It also creates
three risks that the design has to control:

*   **Mistakes reach customers directly.** A wrong multiplier in a script, such
    as 15 instead of 1.5, prices every matching quote wrongly as soon as the
    script runs. No compiler or code review catches it. The change ticket
    review is the only human check, so the database must reject values that are
    clearly invalid before they become live.
*   **Prices must be explainable.** A customer, auditor, or regulator may ask
    why a policy was priced at a given amount months after the fact. By then the
    rules and the policy's base price may have changed. If the system only
    stores the final number, the question cannot be answered.
*   **Stale rules are not acceptable.** Once a change is approved and applied, a
    delay of even a few seconds is noticeable. Common speed-ups, such as holding
    rules in memory and refreshing them on a timer, directly conflict with this.

The Goldfish solution has none of this today. Its only data is a sample
`Products` table. There are no policy tables, no pricing logic, no tests, no
logging, and no authentication.

## Goals

*   **G1. Rule changes need no deployment.** A pricing change made through a
    change-ticket script applies to quotes without a code change, build,
    deployment, or restart.
*   **G2. No stale pricing.** The first quote request that starts after a rule
    change commits uses the new rules. No request ever sees part of a change.
*   **G3. Same input, same price.** For a given policy and rule version, the
    winning rule and the final price are always the same. Ties between rules
    cannot happen, and the behavior when no rule matches is defined.
*   **G4. Every quote can be explained.** Each saved quote records the rule
    version, the rule that matched, the policy values that were evaluated, the
    multiplier, and the final price. Explaining a past quote needs no other
    table.
*   **G5. Rule history cannot be rewritten.** Published rules cannot be updated
    or deleted, and SQL Server itself enforces this, not team habit. Each
    version records the change ticket that approved it.
*   **G6. Invalid rules are rejected when the script runs.** Values that cannot
    be correct, such as a multiplier of zero or below, duplicate priorities in
    one version, or a minimum above a maximum, make the script fail. They never
    reach a live quote.
*   **G7. Pricing decisions can be investigated.** Operators can see which rule
    version the API currently treats as active, and each pricing decision is
    logged with enough detail to trace it.
*   **G8. The quote endpoint is not anonymous.** Only authenticated callers can
    request quotes.

## Non-Goals

*   **A rule-editing screen or in-app approval workflow.** Review already
    happens in the existing change-ticket system. The API stores and evaluates
    approved rules only.
*   **A general rules engine or expression language.** Rules test a fixed set of
    policy fields, and all of a rule's conditions must be true (AND only). No
    OR, no nested conditions, no formulas stored as text, and no third-party
    engine such as Microsoft RulesEngine or NRules.
*   **Combining rules.** Exactly one rule prices a quote. Stacked multipliers,
    added fees, discounts layered on surcharges, and price caps are out of scope.
*   **Caching rules in memory.** Rules are read from the database on every quote.
    Adding a cache is a later decision that must not break G2.
*   **Rate tables for base price.** The base price is stored on each policy. It
    is not looked up from a table of rates by state, industry, or similar.
*   **Multiple currencies.** All amounts are in one currency.
*   **Re-pricing past quotes.** A rule change affects new quotes only. Saved
    quotes keep the price they were given.
*   **Delivering rule data through EF Core migrations.** Migrations change table
    structure only. Rule rows arrive only through change-ticket scripts.

## Open Questions

*   **Policy types**: Which policy types exist? The plan assumes a closed list
    of `Commercial` and `Personal` until the business supplies the real list.
    Adding a type needs a migration (it changes CHECK constraints).
*   **Retention**: How long must quotes and policy history be kept? Nothing is
    purged in this design.
*   **Operations ownership**: Who runs EF Core migrations in production, and
    which DBA owns the database logins and the permission script?
*   **Load**: What are the expected peak quotes per second and rules per
    version? The plan assumes at most tens of quotes per second and at most a
    few hundred rules per version.

## Critical User Journeys (CUJs)

### CUJ 1: Quote a commercial policy over the coverage threshold

*   **Given**: Rule version 1 is active. It contains rule "Large commercial"
    (priority 10, PolicyType = Commercial, MinCoverageLimitExclusive =
    1,000,000, multiplier 1.5) and a catch-all rule (priority 1000, no
    conditions, multiplier 1.0). Policy P has type Commercial, coverage limit
    2,000,000, base price 1,234.57. The caller has an API key with the
    `quotes:create` permission.
*   **When**: The caller sends `POST /api/policies/{P}/quotes`.
*   **Then**: The API returns 201. The quote shows version 1, rule "Large
    commercial", multiplier 1.5, unrounded price 1,851.855, and final price
    1,851.86. A row with those values and the caller's key name is saved in
    `pricing.Quotes`.

### CUJ 2: Boundary value falls through to the catch-all

*   **Given**: The same rules as CUJ 1, and policy Q with type Commercial and
    coverage limit exactly 1,000,000.
*   **When**: The caller requests a quote for Q.
*   **Then**: "Large commercial" does not match, because its minimum is
    exclusive. The catch-all prices the quote at 1.0 times the base price. The
    log entry marks the quote as priced by the catch-all.

### CUJ 3: Analysts publish a change that applies immediately

*   **Given**: A change ticket raising "Large commercial" to 1.6 is approved.
*   **When**: The ticket script calls `pricing.PublishRuleSetVersion` with the
    full rule set, including the catch-all.
*   **Then**: The procedure validates the set and inserts version 2 in one
    transaction. The first quote request that starts after the commit uses
    version 2. Quotes already saved still show version 1.

### CUJ 4: An invalid version is rejected before it goes live

*   **Given**: A ticket script submits a rule set with no catch-all rule, or
    with two rules at priority 10, or with a multiplier of 0.
*   **When**: The script calls `pricing.PublishRuleSetVersion`.
*   **Then**: The procedure raises an error naming the problem, and nothing is
    inserted. Version 1 stays active.

### CUJ 5: A mistaken version is corrected

*   **Given**: Version 2 went live with "Large commercial" at 1.6, but the
    approved value was 1.06.
*   **When**: A new change ticket publishes version 3, a full copy of the rule
    set with 1.06.
*   **Then**: The next quote uses version 3. Version 2 and the quotes priced by
    it stay unchanged in the database, so the mistake and its impact can be
    traced. The active-version endpoint shows version 3.

### CUJ 6: A base-price edit is recorded

*   **Given**: Policy P has base price 1,234.57 and was quoted yesterday.
*   **When**: A caller with `policies:write` sends `PUT /api/policies/{P}` with
    base price 1,300.00.
*   **Then**: The policy row shows 1,300.00. SQL Server's history table keeps
    the earlier row with its validity period. Yesterday's quote still shows
    1,234.57 as the base price it used.

## Technical Plan (Architecture)

### Decisions

| # | Question | Decision |
| --- | --- | --- |
| 1 | What is a version? | A full copy of the rule set under one header row. |
| 2 | Which priority wins? | The lowest number. Priority must be 1 or more. |
| 3 | What if no rule matches? | Every version must contain exactly one catch-all rule, with the highest priority number in that version. |
| 4 | How is the active version chosen? | The newest committed version (highest `VersionNumber`). There are no effective dates and no clock comparison. A change applies as soon as it commits. |
| 5 | How are conditions stored? | Typed columns: `PolicyType`, `MinCoverageLimitExclusive`, `MaxCoverageLimitInclusive`. |
| 6 | Where is the winner picked? | In one SQL query. |
| 7 | Rounding | 2 decimal places, `MidpointRounding.ToEven`, written explicitly. The unrounded value is also stored. |
| 8 | Multiplier range | Greater than 0, no ceiling beyond the column type (`decimal(9,4)`, at most 99,999.9999). |
| 9 | Caller authentication | API keys in an `X-Api-Key` header, checked against stored SHA-256 hashes. Each key has named permissions. |
| 10 | Preventing edits | Database permissions. No login used by the API or by ticket scripts has UPDATE or DELETE on pricing tables. |
| 11 | Policy lifecycle | Create and update endpoints. `Policies` is a SQL Server temporal table. |
| 12 | Publishing | A stored procedure, `pricing.PublishRuleSetVersion`, validates and inserts in one transaction. |

### Components and data flow

```text
                 X-Api-Key
 Caller ───────────────────────► Goldfish.Api
                                  │ ApiKeyAuthenticationHandler (key → name + permissions)
                                  │ PoliciesController / QuotesController / PricingController
                                  ▼
                               Goldfish.Application
                                  │ CreateQuoteHandler: load policy → find match → build quote → save → log
                                  ▼
                               Goldfish.Domain
                                  │ Policy, Quote (price calculation and rounding), PricingRuleMatch
                                  │ IPolicyRepository, IQuoteRepository, IPricingRuleReader
                                  ▼
                               Goldfish.Infrastructure
                                  │ EF Core repositories, SqlPricingRuleReader (one raw SQL query)
                                  ▼
                               SQL Server
                                  dbo.Policies (+ dbo.PoliciesHistory, temporal)
                                  pricing.PricingRuleSetVersions ◄─┐
                                  pricing.PricingRules           ◄─┤ pricing.PublishRuleSetVersion
                                  pricing.Quotes                    │   (EXECUTE only)
                                                                    │
 Change ticket script ──────────────────────────────────────────────┘
```

The layers follow the existing solution. The Domain owns entities, the price
calculation, and repository interfaces. The Application owns use-case
handlers. Infrastructure owns EF Core and SQL. The Api owns HTTP,
authentication, and mapping errors to responses.

### Data model

All money columns are `decimal(18,2)` and all times are `datetime2` in UTC,
matching the existing `Products` table. Ids are `uniqueidentifier` values
created with `Guid.CreateVersion7()`, except where noted.

**`dbo.Policies`** (temporal; history in `dbo.PoliciesHistory`)

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key |
| PolicyType | nvarchar(32) | CHECK in (`Commercial`, `Personal`) |
| CoverageLimit | decimal(18,2) | CHECK > 0 |
| BasePrice | decimal(18,2) | CHECK > 0 |
| CreatedOnUtc | datetime2 | Set by the app from `TimeProvider` |
| PeriodStart, PeriodEnd | datetime2 | Managed by SQL Server for history |

**`pricing.PricingRuleSetVersions`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key, created by the procedure (`NEWSEQUENTIALID()`) |
| VersionNumber | int | Unique. Assigned by the procedure as the current maximum plus 1. |
| ChangeTicketRef | nvarchar(64) | Required, unique |
| ApprovedBy | nvarchar(128) | Required |
| PublishedOnUtc | datetime2 | Default `SYSUTCDATETIME()`. For the record only; never used to choose a version. |

The unique index on `VersionNumber` also serves the active-version lookup.

**`pricing.PricingRules`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key |
| VersionId | uniqueidentifier | Foreign key to versions |
| Priority | int | CHECK >= 1. Unique with VersionId. |
| Name | nvarchar(200) | Required |
| PolicyType | nvarchar(32) NULL | NULL means any type. Otherwise CHECK in the policy type list. |
| MinCoverageLimitExclusive | decimal(18,2) NULL | NULL means no lower limit |
| MaxCoverageLimitInclusive | decimal(18,2) NULL | NULL means no upper limit. CHECK Min < Max when both are set. |
| Multiplier | decimal(9,4) | CHECK > 0 |

A filtered unique index on `VersionId`, where all three condition columns are
NULL, allows at most one catch-all per version. The procedure enforces "exactly
one" and "highest priority number".

**`pricing.Quotes`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key |
| PolicyId | uniqueidentifier | Foreign key to policies |
| VersionId | uniqueidentifier | Foreign key to versions |
| VersionNumber | int | Copy, for reading without a join |
| RuleId | uniqueidentifier | Foreign key to rules |
| RuleName | nvarchar(200) | Copy |
| WasCatchAll | bit | True when the catch-all priced the quote |
| PolicyType, CoverageLimit, BasePrice | as on Policies | Copies of the values evaluated |
| Multiplier | decimal(9,4) | Copy |
| UnroundedPrice | decimal(28,6) | BasePrice × Multiplier |
| FinalPrice | decimal(18,2) | UnroundedPrice rounded to 2 places, ToEven |
| RequestedBy | nvarchar(64) | The API key's name |
| CreatedOnUtc | datetime2 | From `TimeProvider` |

### Choosing the active version

The active version is the committed version with the highest version number:
`SELECT TOP (1) ... ORDER BY VersionNumber DESC`. No clock is compared, so
differences between server clocks cannot delay a change. A version becomes
active at the moment its publishing transaction commits.

A change that must start at a set time, such as midnight on the 1st, is
handled by running its ticket script at that time. This is an accepted risk:
the timing depends on whoever runs the script.

### Finding the winning rule

`SqlPricingRuleReader` runs one parameterized query through
`Database.SqlQueryRaw`:

```sql
WITH active AS (
    SELECT TOP (1) Id, VersionNumber
    FROM pricing.PricingRuleSetVersions
    ORDER BY VersionNumber DESC
)
SELECT a.Id AS VersionId, a.VersionNumber,
       m.Id AS RuleId, m.Name, m.Priority, m.Multiplier, m.IsCatchAll
FROM active a
OUTER APPLY (
    SELECT TOP (1) r.Id, r.Name, r.Priority, r.Multiplier,
           CASE WHEN r.PolicyType IS NULL
                 AND r.MinCoverageLimitExclusive IS NULL
                 AND r.MaxCoverageLimitInclusive IS NULL
                THEN 1 ELSE 0 END AS IsCatchAll
    FROM pricing.PricingRules r
    WHERE r.VersionId = a.Id
      AND (r.PolicyType IS NULL OR r.PolicyType = @policyType)
      AND (r.MinCoverageLimitExclusive IS NULL OR @coverageLimit > r.MinCoverageLimitExclusive)
      AND (r.MaxCoverageLimitInclusive IS NULL OR @coverageLimit <= r.MaxCoverageLimitInclusive)
    ORDER BY r.Priority ASC
) m;
```

The query has three outcomes:

*   **No row.** No version has been published yet. The result is
    `Pricing.NoActiveVersion`, returned as 503.
*   **A row with a null RuleId.** The active version has no matching rule,
    which the catch-all requirement should make impossible. The result is
    `Pricing.InvalidActiveVersion`, returned as 500 and logged as an error.
*   **A row with a rule.** This is the match.

Policy types come from a closed list that is checked on both sides, so the
database collation cannot change which rule matches.

The rules are read on every request with no cache. The query and the quote
insert are separate statements. If a new version commits between them, the
quote still records the version it actually used, so it stays correct.

### Publishing a version

`pricing.PublishRuleSetVersion` is created by an EF Core migration using raw
SQL. Its parameters:

*   `@ChangeTicketRef`
*   `@ApprovedBy`
*   `@Rules`, a table-valued parameter of type `pricing.PricingRuleInput` with
    Priority, Name, PolicyType, MinCoverageLimitExclusive,
    MaxCoverageLimitInclusive and Multiplier

Inside one `SERIALIZABLE` transaction, the procedure does the following:

1.  Rejects an empty rule list.
2.  Rejects the call unless exactly one rule has all three conditions NULL, and
    that rule's priority is higher than every other rule's.
3.  Gives the version the next version number. The `SERIALIZABLE` lock
    prevents two publishes from taking the same number.
4.  Inserts the header and all rules. The table CHECK constraints and unique
    indexes reject invalid values such as a multiplier of 0, a duplicate
    priority, or a minimum above a maximum. Any failure rolls back everything.
5.  Returns the new version number.

The database runs with `READ_COMMITTED_SNAPSHOT ON`. Quote queries read the
last committed state and are never blocked by a publish in progress, so they
never see a half-inserted version.

### Database permissions

Permissions are applied by a reviewed script, `db/scripts/grant-permissions.sql`,
which a DBA runs. It is not part of the migrations, because logins are managed
per environment.

| Login | Rights |
| --- | --- |
| API (`goldfish_api`) | SELECT on `pricing.PricingRuleSetVersions` and `pricing.PricingRules`. SELECT and INSERT on `pricing.Quotes`. SELECT, INSERT and UPDATE on `dbo.Policies`. SELECT on `dbo.PoliciesHistory`. |
| Ticket scripts (`goldfish_pricing_publisher`) | EXECUTE on `pricing.PublishRuleSetVersion` only. Ownership chaining lets the procedure insert rows the login cannot insert directly. |
| Migrations (`goldfish_migrator`) | Schema changes. Used only by deployments. |

A companion script, `db/scripts/verify-permissions.sql`, lists the actual
grants so a reviewer can check them. **Accepted risk:** a login with db_owner or
sysadmin rights can still update or delete rows.

### API

| Method and route | Permission | Success | Errors |
| --- | --- | --- | --- |
| `POST /api/policies` | `policies:write` | 201 + PolicyResponse | 400 validation |
| `GET /api/policies/{id}` | `policies:read` | 200 | 404 `Policy.NotFound` |
| `PUT /api/policies/{id}` | `policies:write` | 200 | 400, 404 |
| `POST /api/policies/{policyId}/quotes` | `quotes:create` | 201 + QuoteResponse | 404 `Policy.NotFound`, 503 `Pricing.NoActiveVersion`, 500 `Pricing.InvalidActiveVersion` |
| `GET /api/quotes/{id}` | `quotes:read` | 200 | 404 `Quote.NotFound` |
| `GET /api/pricing/active-version` | `pricing:read` | 200: active version number, its change ticket reference, and when it was published | 503 `Pricing.NoActiveVersion` |

A missing or unknown key returns 401. A key without the required permission
returns 403.

Every `POST` to the quotes route creates a new quote. There is no idempotency
key: a repeated request is a second quote, and the audit trail records both.

`QuoteResponse` contains:

*   Id
*   PolicyId
*   VersionNumber
*   RuleName
*   WasCatchAll
*   BasePrice
*   Multiplier
*   UnroundedPrice
*   FinalPrice
*   CreatedOnUtc

### Error handling

`Error` gains an `ErrorType` (`Validation`, `NotFound`, `Unavailable`,
`Failure`). A single Api extension maps `ErrorType` to the HTTP status codes in
the API table and builds ProblemDetails, so controllers no longer switch on
code strings. Existing `ProductErrors` become `Validation` and `NotFound`.

### API key authentication

Keys are configured under `ApiKeys` as a list of entries, each with:

*   `Name`
*   `KeyHash` (hex SHA-256 of the key)
*   `Permissions` (a list of permission strings)

Only hashes are stored. Development hashes go in user secrets, and production
hashes come from environment configuration.

`ApiKeyAuthenticationHandler` hashes the incoming header value and compares it
with each stored hash using `CryptographicOperations.FixedTimeEquals`. On a
match it creates a principal with the key's name and one `permission` claim per
permission. Each permission is an authorization policy, applied with
`[Authorize(Policy = ...)]`. Rotating a key means adding the new entry, moving
callers to it, and removing the old entry, with no code change.

### Logging and visibility (G7)

`CreateQuoteHandler` writes one structured log entry per quote with:

*   QuoteId
*   PolicyId
*   VersionNumber
*   RuleId
*   WasCatchAll
*   Multiplier
*   FinalPrice
*   RequestedBy

Quotes priced by the catch-all are logged at Warning level so gaps in the rules
are visible. A `System.Diagnostics.Metrics` counter, `goldfish.quotes.created`,
is tagged with the version number and the catch-all flag. The
`/api/pricing/active-version` endpoint shows operators which version is
active and which change ticket published it.

### Accepted risks

*   **Large typos pass.** A multiplier of 15 instead of 1.5 passes every
    database check (decision 8). The ticket review is the only guard. Detection
    relies on the quote log and the per-version metric.
*   **Privileged logins.** db_owner and sysadmin logins can bypass the
    no-edit permissions (decision 10).
*   **Unreviewed base-price edits.** A base-price change through
    `PUT /api/policies` takes effect without a change ticket. It is recorded
    in the temporal history and limited to keys with `policies:write`.
*   **Last write wins.** Two simultaneous policy updates overwrite each other.
    Both are kept in the history table.
*   **Manual timing for scheduled changes.** A change that must start at a set
    time depends on someone running its ticket script at that time (decision
    4).

## Alternatives Considered

### Alternative A: Pricing rules written in C# code

*   **Pros**: It is the simplest option. The compiler checks it, and pricing
    changes are unit-tested and reviewed like any other code.
*   **Cons**: Every monthly change needs a developer, a build, and a deployment.
*   **Reason for Rejection**: Rejected by author. Business analysts change
    pricing monthly, and changes must not wait for a code release (G1).

### Alternative B: Microsoft RulesEngine

*   **Pros**: Rules are stored as JSON, so they can change without a
    deployment. It supports any combination of conditions.
*   **Cons**:
    *   Rules contain lambda expressions as text, which
        System.Linq.Dynamic.Core evaluates at runtime. That is code stored as
        data: an error shows up only when the rule runs, not when it is saved.
    *   It has no built-in "highest priority wins", so results must be sorted
        afterward.
    *   It keeps compiled workflows in memory. That conflicts with G2 unless
        the workflows are rebuilt on every request.
    *   Its expression library had a remote code execution vulnerability
        (CVE-2023-32571, fixed in System.Linq.Dynamic.Core 1.3.0).
*   **Reason for Rejection**: Rejected in review. Its flexibility serves needs
    this design does not have (see the non-goal "no expression language"), and
    it weakens G2 and G6.

### Alternative C: NRules

*   **Pros**: A mature forward-chaining rules engine with rules written as
    compiled C# classes.
*   **Cons**:
    *   Because rules are code, every change needs a deployment.
    *   Loading rules from the database would mean building rule models at
        runtime.
    *   Its chaining model does far more than picking one winning rule needs.
*   **Reason for Rejection**: Rejected in review. In its normal use it fails G1,
    and if adapted to load rules from the database it is far more machinery
    than this needs.

### Alternative D: Rule conditions in a JSON column

*   **Pros**: New condition fields need no schema change.
*   **Cons**:
    *   CHECK constraints cannot validate the contents.
    *   It is harder to query for audit.
    *   Ticket reviewers read JSON instead of columns.
*   **Reason for Rejection**: Rejected in review. It fails G6.

### Alternative E: An in-memory rule cache with push invalidation (SqlDependency or Service Broker)

*   **Pros**: Fewer database reads per quote.
*   **Cons**: It adds a messaging channel that can fail silently and leave
    servers on stale rules. A cache that refreshes on a timer breaks G2
    outright.
*   **Reason for Rejection**: Rejected in review. One indexed query per quote
    is cheap at the expected load. If a cache is ever needed, a cache keyed by
    version number meets G2 without push messaging: it re-reads the rules only
    when the active version id changes.

### Alternative F: A separate version for each rule

*   **Pros**: Scripts are small and show exactly what changed.
*   **Cons**:
    *   A plain unique index cannot prevent ties, because two rules with the
        same priority conflict only when both are active at once.
    *   Reproducing a quote means rebuilding the set of rules that was active
        at that moment.
    *   Whether a change is applied all at once depends on each script.
*   **Reason for Rejection**: Rejected by author. With a full copy of the rule
    set per version (decision 1), a constraint makes ties impossible and one
    version id reproduces any quote.

### Alternative G: Versions that hold only the changes since the last version

*   **Pros**: Each publish is applied all at once, and scripts stay small.
*   **Cons**:
    *   The active rule set must be worked out from the whole history on every
        quote.
    *   Preventing ties needs trigger logic.
    *   A long history eventually needs a stored snapshot, which ends up much
        like a full copy anyway.
*   **Reason for Rejection**: Rejected by author, in favor of full copies of the
    rule set.

### Alternative H: The highest priority number wins

*   **Pros**: There is always room above the current top rule.
*   **Cons**: Anyone used to "priority 1 means most important" would write
    rules backwards, and no database check catches that.
*   **Reason for Rejection**: Rejected by author. The lowest number wins
    (decision 2).

### Alternative I: No match returns an error, or the catch-all is optional

*   **Pros**: Gaps in the rules show up straight away as failed requests.
*   **Cons**:
    *   A script that narrows a condition becomes an outage for every policy it
        no longer covers.
    *   An optional catch-all makes behavior differ between versions.
*   **Reason for Rejection**: Rejected by author. Every version must contain
    exactly one catch-all (decision 3). Quotes priced by the catch-all are
    logged at Warning level, so the gaps it covers stay visible.

### Alternative J: Future-dated versions, with SQL Server's clock deciding

*   **Pros**:
    *   A ticket script can run days early and switch on at an exact time.
    *   One clock serves every API server.
*   **Cons**:
    *   It adds an effective-date column, a clock comparison in every quote
        query, and a rule that effective times must not go backwards.
    *   Operators need a view of versions that are scheduled but not yet active.
    *   The timing logic cannot be tested by moving a fake clock.
*   **Reason for Rejection**: Rejected by author while reconciling the draft
    with the concept brief. A change applies as soon as it commits (decision 4).
    Scheduled changes are handled by running the script at the right time.

### Alternative K: Future-dated versions, with the API server's clock deciding

*   **Pros**: The activation logic can be unit-tested by moving a fake clock.
*   **Cons**:
    *   An API server whose clock is behind SQL Server's would not see an
        immediate change until its clock caught up.
    *   Servers could disagree with each other at each scheduled switch.
*   **Reason for Rejection**: Rejected by author. It conflicts with the
    requirement that a delay of even a few seconds is unacceptable, and future
    dating was dropped altogether (decision 4).

### Alternative L: Generic condition rows (field, operator, value)

*   **Pros**: New operators and values need no schema change.
*   **Cons**:
    *   Validating which value column applies to which field is complex.
    *   One rule spans several rows, which makes it harder to review.
    *   With only two condition fields, most of this machinery would go unused.
*   **Reason for Rejection**: Rejected by author, in favor of typed columns
    (decision 5).

### Alternative M: Pick the winning rule in C#

*   **Pros**: Every matching edge case can be unit-tested without a database,
    and text comparison follows one set of C# rules.
*   **Cons**: Every quote loads every rule in the active version.
*   **Reason for Rejection**: Rejected by author. Matching runs in one SQL query
    (decision 6), and integration tests against a real SQL Server cover the
    edge cases.

### Alternative N: Round halves up, or store the exact price

*   **Pros**: Rounding halves up matches what spreadsheets do. Storing the exact
    price loses nothing.
*   **Cons**:
    *   Rounding halves up creeps slightly upward over many quotes, and it is
        not the .NET default.
    *   Storing the exact price forces every system that reads it to round the
        same way.
*   **Reason for Rejection**: Rejected by author, in favor of 2 decimal places
    with banker's rounding (decision 7). The unrounded value is also stored, so
    a one-cent difference can be explained.

### Alternative O: A ceiling on multipliers, or surcharges only

*   **Pros**:
    *   A ceiling would make the database reject an extra digit, such as 15
        instead of 1.5.
    *   Allowing surcharges only would also reject a misplaced decimal point,
        such as 0.15.
*   **Cons**: A real rule above the ceiling, or any discount, would need a
    migration.
*   **Reason for Rejection**: Rejected by author. A multiplier only has to be
    above 0 (decision 8). Large typos are an accepted risk.

### Alternative P: JWT bearer tokens from an identity provider

*   **Pros**: Each calling system, and each person, has its own identity, and
    the API stores no secrets.
*   **Cons**: It needs an app registration with an identity provider, and the
    integration tests need a test token issuer.
*   **Reason for Rejection**: Rejected by author, in favor of API keys
    (decision 9).

### Alternative Q: Block edits with triggers or ledger tables

*   **Pros**:
    *   Triggers live in source control and block every login.
    *   Ledger tables can't be changed even by db_owner, and they can prove
        that no row was altered.
*   **Cons**:
    *   Triggers block legitimate cleanup, and db_owner can disable them.
    *   Ledger tables need SQL Server 2022 or Azure SQL, are created with raw
        SQL, and can never be purged.
*   **Reason for Rejection**: Rejected by author, in favor of database
    permissions (decision 10).

### Alternative R: Create-only policies, or policies loaded outside the API

*   **Pros**: A smaller scope. If policies can only be created, they never
    change, so no history mechanism is needed.
*   **Cons**:
    *   With create-only policies, a base price can't be corrected during the
        policy term.
    *   Loading policies from outside the API skips validation, and leaves no
        API path for creating test data.
*   **Reason for Rejection**: Rejected by author, in favor of create and update
    endpoints with a temporal table (decision 11).

### Alternative S: Ticket scripts insert rows directly from a reviewed template

*   **Pros**: Scripts are plain INSERT statements, with no stored procedure.
*   **Cons**:
    *   Two things depend on people following the template: that the whole
        change is applied at once, and that the version has exactly one
        catch-all rule.
    *   A broken version shows up only as failed quotes.
*   **Reason for Rejection**: Rejected by author. It conflicts with G2 and G6.
    Publishing goes through `pricing.PublishRuleSetVersion` (decision 12).

### Alternative T: Other sources for the base price

*   **Pros**:
    *   A fixed $100,000 base for every policy is the simplest option.
    *   A base price sent with each request needs no storage.
    *   An existing outside system would let you reuse its data.
*   **Cons**: None of them fits how base prices actually work.
*   **Reason for Rejection**: Rejected by author. The base price varies by
    policy, must come from data stored in this system, and is kept in new
    Goldfish tables.

### Alternative U: A rule-editing screen and an in-app approval workflow

*   **Pros**: Analysts could edit rules without writing SQL.
*   **Cons**: It is a large separate build, and it duplicates the existing
    change-ticket review.
*   **Reason for Rejection**: Rejected by author. Review already happens in the
    change-ticket system.

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **Security**
    *   API keys are never stored in plain text. Only SHA-256 hashes are kept
        in configuration, and incoming keys are compared in constant time.
    *   The pricing tables do not allow UPDATE or DELETE for the API login or
        the ticket login (decision 10).
    *   The existing `ProductsController` sample stays anonymous. No global
        authorization policy is added in this design, so new controllers must
        each carry an `[Authorize]` attribute.
*   **Performance**
    *   One quote costs three queries: load the policy, run the matching query,
        and insert the quote.
    *   The matching query uses the unique index on `VersionNumber` and the
        unique index on (`VersionId`, `Priority`). At a few hundred rules per
        version it reads a few pages at most.
    *   `READ_COMMITTED_SNAPSHOT` stops quote reads from waiting on a publish
        in progress.
    *   Assumed load: tens of quotes per second.
*   **Rollout**
    *   The order is:
        1.  Apply the migration, which adds tables, the procedure, and
            `READ_COMMITTED_SNAPSHOT`.
        2.  The DBA runs `db/scripts/grant-permissions.sql` and
            `db/scripts/verify-permissions.sql`.
        3.  Deploy the API.
        4.  The first change ticket runs `db/tickets/0001-initial-rule-set.sql`.
    *   Until step 4 runs, quote requests return 503 `Pricing.NoActiveVersion`,
        which is safe and clearly reported.
    *   Turning on `READ_COMMITTED_SNAPSHOT` uses `WITH ROLLBACK IMMEDIATE`,
        which ends other open connections. Apply the migration during a
        deployment window.
*   **Rollback**
    *   Rolling back the API binaries is safe, because the old API ignores the
        new tables.
    *   The migration's `Down` drops the tables, which destroys policies,
        rules, and quote history. It must never be run in production once data
        exists.
    *   A bad rule version is "rolled back" by publishing a new version.

--------------------------------------------------------------------------------

### Additional Resources

*   `./concepts_brief.md`: the brainstorm and Goldfish review this design
    builds on.
*   EF Core temporal tables:
    `https://learn.microsoft.com/ef/core/providers/sql-server/temporal-tables`
*   SQL Server ownership chaining and permissions on stored procedures.

### Future Improvements

*   **OpenTelemetry tracing** for ASP.NET Core and EF Core requests.
*   **A cache keyed by version number**, if quote load ever makes the matching
    query a bottleneck.
*   **A diff report for ticket reviewers** that compares a proposed rule set
    with the active version.
*   **An alert on multiplier jumps** between consecutive versions, to cover the
    accepted risk of large typos.
*   **Optimistic concurrency** (`rowversion` with `If-Match`) on policy updates.
*   **An idempotency key** on quote creation.

--------------------------------------------------------------------------------

## Detailed Implementation Plan

Paths are relative to the repository root, `C:\Goldfish`. Package versions are
pinned exactly to the latest stable release compatible with `net10.0` when the
work is done. EF Core packages stay at 10.0.15 to match the existing
references.

### Target Files to Create/Modify

#### Domain

##### 1. `src/Goldfish.Domain/Abstractions/Result.cs` (Modify)

*   **Type / Scope**: Domain abstraction.
*   **Proposed Changes**:
    *   Add `public enum ErrorType { Failure, Validation, NotFound, Unavailable }`.
    *   Change `Error` to
        `record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)`.
    *   Add static factories `Error.Validation(code, description)`,
        `Error.NotFound(...)`, `Error.Unavailable(...)`, and
        `Error.Failure(...)`.
    *   `Error.None` is unchanged.
*   **Why**: The API needs one mapping from error kind to HTTP status
    (Technical Plan, Error handling) instead of controllers switching on code
    strings.

##### 2. `src/Goldfish.Domain/Products/ProductErrors.cs` (Modify)

*   **Type / Scope**: Domain errors.
*   **Proposed Changes**: Use `Error.NotFound` for `NotFound`, and
    `Error.Validation` for `InvalidName` and `InvalidPrice`.
*   **Why**: Keeps the existing endpoint's status codes the same under the
    shared mapping.

##### 3. `src/Goldfish.Domain/Policies/PolicyType.cs` (Create)

*   **Type / Scope**: Domain enum.
*   **Proposed Changes**: `public enum PolicyType { Commercial, Personal }`.
*   **Why**: The closed list of policy types, stored as text. It must match the
    CHECK constraints in the migration.

##### 4. `src/Goldfish.Domain/Policies/Policy.cs` (Create)

*   **Type / Scope**: Domain aggregate, `sealed`, derived from `Entity`.
*   **Proposed Changes**:
    *   Properties with private setters: `Type`, `CoverageLimit`, `BasePrice`,
        `CreatedOnUtc`.
    *   `static Result<Policy> Create(PolicyType type, decimal coverageLimit, decimal basePrice, DateTime utcNow)`
        uses `Guid.CreateVersion7()`.
    *   `Result Update(PolicyType type, decimal coverageLimit, decimal basePrice)`.
    *   Both methods reject a coverage limit of 0 or below, a base price of 0
        or below, an undefined enum value, and any value with more than 2
        decimal places.
    *   A private parameterless constructor for EF Core.
*   **Why**: Policies hold the inputs to pricing (decision 11). The pattern
    follows `Product.Create`.

##### 5. `src/Goldfish.Domain/Policies/PolicyErrors.cs` (Create)

*   **Type / Scope**: Domain errors.
*   **Proposed Changes**:
    *   `NotFound` (`Policy.NotFound`, NotFound).
    *   `InvalidType`, `InvalidCoverageLimit`, `InvalidBasePrice` (Validation).
*   **Why**: Stable error codes for the policy endpoints.

##### 6. `src/Goldfish.Domain/Policies/IPolicyRepository.cs` (Create)

*   **Type / Scope**: Repository interface.
*   **Proposed Changes**:
    *   `Task<Policy?> GetByIdAsync(Guid id, CancellationToken ct)` returns a
        tracked entity, so updates can be saved.
    *   `void Add(Policy policy)`.
*   **Why**: Per-aggregate repository, as with Products.

##### 7. `src/Goldfish.Domain/Pricing/PricingRuleMatch.cs` (Create)

*   **Type / Scope**: Domain value (`sealed record`).
*   **Proposed Changes**:
    `PricingRuleMatch(Guid VersionId, int VersionNumber, Guid RuleId, string RuleName, int Priority, decimal Multiplier, bool IsCatchAll)`.
*   **Why**: The winning rule as returned by the matching query (decision 6).

##### 8. `src/Goldfish.Domain/Pricing/IPricingRuleReader.cs` (Create)

*   **Type / Scope**: Domain interface.
*   **Proposed Changes**:
    `Task<Result<PricingRuleMatch>> FindMatchAsync(PolicyType type, decimal coverageLimit, CancellationToken ct)`.
    *   Returns `PricingErrors.NoActiveVersion` when no version has started.
    *   Returns `PricingErrors.InvalidActiveVersion` when no rule matched.
*   **Why**: Keeps the matching SQL behind an interface that the Application
    layer can call.

##### 9. `src/Goldfish.Domain/Pricing/PricingErrors.cs` (Create)

*   **Type / Scope**: Domain errors.
*   **Proposed Changes**:
    *   `NoActiveVersion` (`Pricing.NoActiveVersion`, Unavailable).
    *   `InvalidActiveVersion` (`Pricing.InvalidActiveVersion`, Failure).
*   **Why**: Stable codes for the two pricing failure cases (Technical Plan,
    Finding the winning rule).

##### 10. `src/Goldfish.Domain/Quotes/Quote.cs` (Create)

*   **Type / Scope**: Domain entity, `sealed`, derived from `Entity`.
*   **Proposed Changes**:
    *   Properties, all private set:
        *   `PolicyId`
        *   `VersionId`, `VersionNumber`
        *   `RuleId`, `RuleName`
        *   `WasCatchAll`
        *   `PolicyType`, `CoverageLimit`, `BasePrice`
        *   `Multiplier`
        *   `UnroundedPrice`, `FinalPrice`
        *   `RequestedBy`
        *   `CreatedOnUtc`
    *   `static Quote Create(Policy policy, PricingRuleMatch match, string requestedBy, DateTime utcNow)`:
        *   computes `UnroundedPrice = policy.BasePrice * match.Multiplier`;
        *   computes `FinalPrice = Math.Round(UnroundedPrice, 2, MidpointRounding.ToEven)`,
            with the rounding mode always written out, never left to the
            default;
        *   copies the policy values onto the quote.
*   **Why**: The quote snapshot (G4) and rounding (decision 7) live in one
    unit-testable place.

##### 11. `src/Goldfish.Domain/Quotes/QuoteErrors.cs` (Create)

*   **Type / Scope**: Domain errors.
*   **Proposed Changes**: `NotFound` (`Quote.NotFound`, NotFound).
*   **Why**: Error for `GET /api/quotes/{id}`.

##### 12. `src/Goldfish.Domain/Quotes/IQuoteRepository.cs` (Create)

*   **Type / Scope**: Repository interface.
*   **Proposed Changes**:
    *   `Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct)`, read
        without tracking.
    *   `void Add(Quote quote)`.
*   **Why**: Per-aggregate repository.

#### Application

##### 13. `src/Goldfish.Application/Goldfish.Application.csproj` (Modify)

*   **Type / Scope**: Build configuration.
*   **Proposed Changes**: Add `Microsoft.Extensions.Logging.Abstractions` and
    `Microsoft.Extensions.Diagnostics.Abstractions` (for `IMeterFactory`),
    both 10.0.x.
*   **Why**: Structured logging and metrics in the quote handler (G7).

##### 14. `src/Goldfish.Application/Policies/PolicyResponse.cs` (Create)

*   **Type / Scope**: Response DTO.
*   **Proposed Changes**:
    `PolicyResponse(Guid Id, string PolicyType, decimal CoverageLimit, decimal BasePrice, DateTime CreatedOnUtc)`
    and an internal `FromEntity`.
*   **Why**: Response shape for the policy endpoints.

##### 15. `src/Goldfish.Application/Policies/PolicyTypeParser.cs` (Create)

*   **Type / Scope**: Internal static helper.
*   **Proposed Changes**: `Result<PolicyType> Parse(string value)`.
    *   Accepts only the exact, case-sensitive names `Commercial` and
        `Personal`.
    *   Rejects numbers and anything else with `PolicyErrors.InvalidType`.
*   **Why**: `Enum.TryParse` accepts numeric strings and can ignore case.
    Either would break the closed list.

##### 16. `src/Goldfish.Application/Policies/CreatePolicy/CreatePolicyHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `CreatePolicyCommand(string PolicyType, decimal CoverageLimit, decimal BasePrice)`.
    *   The handler parses the type, calls `Policy.Create` with
        `TimeProvider`, adds the policy, saves, and returns
        `Result<PolicyResponse>`.
*   **Why**: `POST /api/policies`.

##### 17. `src/Goldfish.Application/Policies/UpdatePolicy/UpdatePolicyHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `UpdatePolicyCommand(Guid Id, string PolicyType, decimal CoverageLimit, decimal BasePrice)`.
    *   The handler loads the tracked policy (`NotFound` if missing), parses
        the type, calls `Update`, and saves. The last write wins.
*   **Why**: `PUT /api/policies/{id}`. SQL Server's temporal table records the
    old row.

##### 18. `src/Goldfish.Application/Policies/GetPolicyById/GetPolicyByIdHandler.cs` (Create)

*   **Type / Scope**: Query and handler.
*   **Proposed Changes**: `GetPolicyByIdQuery(Guid Id)` returns
    `Result<PolicyResponse>`.
*   **Why**: `GET /api/policies/{id}`, and the target of `CreatedAtAction`.

##### 19. `src/Goldfish.Application/Quotes/QuoteResponse.cs` (Create)

*   **Type / Scope**: Response DTO.
*   **Proposed Changes**: Fields as listed in the Technical Plan, plus an
    internal `FromEntity`.
*   **Why**: The response explains the price (G4).

##### 20. `src/Goldfish.Application/Quotes/QuoteMetrics.cs` (Create)

*   **Type / Scope**: Singleton metrics holder.
*   **Proposed Changes**:
    *   Creates the meter `Goldfish.Pricing` from `IMeterFactory`.
    *   Exposes `Counter<long>` `goldfish.quotes.created`.
    *   `Record(int versionNumber, bool wasCatchAll)` adds 1, tagged
        `version` and `catch_all`.
*   **Why**: Shows the catch-all rate and per-version volume (G7, decision 3).

##### 21. `src/Goldfish.Application/Quotes/CreateQuote/CreateQuoteHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `CreateQuoteCommand(Guid PolicyId, string RequestedBy)`.
    *   Steps:
        1.  Load the policy (`Policy.NotFound`).
        2.  Call `IPricingRuleReader.FindMatchAsync`, passing errors through.
        3.  Call `Quote.Create` with `TimeProvider`.
        4.  Add the quote, then call `SaveChangesAsync` once, with no explicit
            transaction.
        5.  Log one structured entry: Information, or Warning when
            `WasCatchAll` is true. Log an Error for `InvalidActiveVersion`.
        6.  Record the metric.
        7.  Return `Result<QuoteResponse>`.
*   **Why**: The quote use case. A single save avoids the conflict between
    `EnableRetryOnFailure` and explicit transactions.

##### 22. `src/Goldfish.Application/Quotes/GetQuoteById/GetQuoteByIdHandler.cs` (Create)

*   **Type / Scope**: Query and handler.
*   **Proposed Changes**: `GetQuoteByIdQuery(Guid Id)` returns
    `Result<QuoteResponse>`.
*   **Why**: `GET /api/quotes/{id}`, and the target of `CreatedAtAction`.

##### 23. `src/Goldfish.Application/Pricing/GetActiveVersion/ActiveVersionStatus.cs` (Create)

*   **Type / Scope**: Response DTO.
*   **Proposed Changes**:
    `ActiveVersionStatus(int VersionNumber, string ChangeTicketRef, DateTime PublishedOnUtc)`.
*   **Why**: The operator view of pricing state (G7, CUJ 5).

##### 24. `src/Goldfish.Application/Pricing/GetActiveVersion/IPricingStatusReader.cs` (Create)

*   **Type / Scope**: Application read interface.
*   **Proposed Changes**:
    `Task<Result<ActiveVersionStatus>> GetAsync(CancellationToken ct)`. Returns
    `PricingErrors.NoActiveVersion` when no version has started.
*   **Why**: An operational read model, kept out of the Domain.

##### 25. `src/Goldfish.Application/Pricing/GetActiveVersion/GetActiveVersionHandler.cs` (Create)

*   **Type / Scope**: Query handler.
*   **Proposed Changes**: Passes the call through to `IPricingStatusReader`.
*   **Why**: Keeps the controller-to-handler pattern consistent.

##### 26. `src/Goldfish.Application/DependencyInjection.cs` (Modify)

*   **Type / Scope**: DI registration.
*   **Proposed Changes**:
    *   `AddScoped` for each new handler (files 16–18, 21, 22, 25).
    *   `AddSingleton<QuoteMetrics>()`.
    *   `AddMetrics()` if it is not registered already.
*   **Why**: This solution registers handlers explicitly and has no assembly
    scanning.

#### Infrastructure

##### 27. `src/Goldfish.Infrastructure/Persistence/ApplicationDbContext.cs` (Modify)

*   **Type / Scope**: EF Core context.
*   **Proposed Changes**: Add `DbSet<Policy> Policies`, `DbSet<Quote> Quotes`,
    and internal sets for the two pricing records (files 30–31).
*   **Why**: Lets migrations create the tables and the repositories query them.

##### 28. `src/Goldfish.Infrastructure/Persistence/Configurations/PolicyConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `dbo.Policies`, made temporal with
        `IsTemporal(t => { HasPeriodStart("PeriodStart"); HasPeriodEnd("PeriodEnd"); UseHistoryTable("PoliciesHistory"); })`.
    *   `Type` maps to column `PolicyType`, `nvarchar(32)`, with
        `HasConversion<string>()`.
    *   CHECK constraints: `PolicyType IN ('Commercial','Personal')`,
        `CoverageLimit > 0`, `BasePrice > 0`.
    *   Money precision `(18,2)`.
    *   `CreatedOnUtc` as `datetime2` with the UTC converter used in
        `ProductConfiguration`.
    *   `ValueGeneratedNever` on `Id`.
*   **Why**: Decision 11 and the data model.

##### 29. `src/Goldfish.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.Quotes`, with columns and precisions as in the data
        model.
    *   Foreign keys to `Policies`, `PricingRuleSetVersions`, and
        `PricingRules`, all with `DeleteBehavior.Restrict`.
    *   An index on `PolicyId`.
    *   The UTC converter on `CreatedOnUtc`.
*   **Why**: The quote snapshot table (G4).

##### 30. `src/Goldfish.Infrastructure/Persistence/Pricing/PricingRuleSetVersionRecord.cs` (Create)

*   **Type / Scope**: Internal persistence class. It is not a domain entity,
    because the API never creates versions.
*   **Proposed Changes**: Properties as in the data model, with a navigation
    property to its rules.
*   **Why**: EF Core needs a mapped type to create and migrate the table.

##### 31. `src/Goldfish.Infrastructure/Persistence/Pricing/PricingRuleRecord.cs` (Create)

*   **Type / Scope**: Internal persistence class.
*   **Proposed Changes**: Properties as in the data model.
*   **Why**: Same reason as file 30.

##### 32. `src/Goldfish.Infrastructure/Persistence/Configurations/PricingRuleSetVersionConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.PricingRuleSetVersions`.
    *   `Id` with default `NEWSEQUENTIALID()`.
    *   Unique indexes on `VersionNumber` and `ChangeTicketRef`.
    *   `PublishedOnUtc` with default `SYSUTCDATETIME()` and the UTC
        converter.
*   **Why**: The data model and active-version lookup.

##### 33. `src/Goldfish.Infrastructure/Persistence/Configurations/PricingRuleConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.PricingRules`.
    *   A unique index on (`VersionId`, `Priority`).
    *   A filtered unique index on `VersionId`, with the filter
        `[PolicyType] IS NULL AND [MinCoverageLimitExclusive] IS NULL AND [MaxCoverageLimitInclusive] IS NULL`.
    *   CHECK constraints:
        *   `Priority >= 1`
        *   `Multiplier > 0`
        *   `PolicyType IS NULL OR PolicyType IN ('Commercial','Personal')`
        *   `MinCoverageLimitExclusive IS NULL OR MaxCoverageLimitInclusive IS NULL OR MinCoverageLimitExclusive < MaxCoverageLimitInclusive`
    *   Precisions as in the data model.
*   **Why**: G3 and G6, plus decisions 2, 3, 5, and 8.

##### 34. `src/Goldfish.Infrastructure/Persistence/Migrations/<timestamp>_AddPolicyPricing.cs` (Create, generated then edited)

*   **Type / Scope**: EF Core migration.
*   **Proposed Changes**:
    *   Generated with
        `dotnet ef migrations add AddPolicyPricing -p src/Goldfish.Infrastructure -s src/Goldfish.Api -o Persistence/Migrations`.
        This creates the `pricing` schema, the tables, the indexes, and the
        temporal table.
    *   Then append `migrationBuilder.Sql` calls for:
        1.  `CREATE TYPE pricing.PricingRuleInput AS TABLE (...)`.
        2.  `CREATE PROCEDURE pricing.PublishRuleSetVersion`, with the
            validation steps 1–5 from the Technical Plan. It uses
            `SET XACT_ABORT ON` and `SET TRANSACTION ISOLATION LEVEL SERIALIZABLE`,
            and raises errors with `THROW 50001`–`50005` and a clear message
            for each check.
        3.  `ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE`,
            with `suppressTransaction: true`.
    *   `Down` drops the procedure, then the type, then the tables. It does not
        turn `READ_COMMITTED_SNAPSHOT` off.
*   **Why**: Schema, publishing (decision 12), and reads that never see a
    partial publish (G2). Rule data is deliberately not included (non-goal).

##### 35. `src/Goldfish.Infrastructure/Persistence/Migrations/<timestamp>_AddPolicyPricing.Designer.cs` and `ApplicationDbContextModelSnapshot.cs` (Create / Modify, generated)

*   **Type / Scope**: EF Core generated files.
*   **Proposed Changes**: Generated by `dotnet ef`. Do not edit by hand.
*   **Why**: EF migration bookkeeping.

##### 36. `src/Goldfish.Infrastructure/Repositories/PolicyRepository.cs` (Create)

*   **Type / Scope**: `internal sealed` repository.
*   **Proposed Changes**: `GetByIdAsync` reads with tracking (needed for
    updates). `Add` adds the policy.
*   **Why**: Implements `IPolicyRepository`.

##### 37. `src/Goldfish.Infrastructure/Repositories/QuoteRepository.cs` (Create)

*   **Type / Scope**: `internal sealed` repository.
*   **Proposed Changes**: `GetByIdAsync` reads with `AsNoTracking`. `Add` adds
    the quote.
*   **Why**: Implements `IQuoteRepository`.

##### 38. `src/Goldfish.Infrastructure/Pricing/SqlPricingRuleReader.cs` (Create)

*   **Type / Scope**: `internal sealed` implementation of `IPricingRuleReader`.
*   **Proposed Changes**:
    *   Runs the Technical Plan's matching query through
        `Database.SqlQuery<MatchRow>(FormattableString)`, so values are passed
        as parameters.
    *   `@policyType` is passed as the enum's string name.
    *   Calls `ToListAsync` and does not compose further, because EF cannot
        wrap a query that starts with `WITH`.
    *   An empty list maps to `NoActiveVersion`. A row with a null `RuleId`
        maps to `InvalidActiveVersion`. Otherwise the row maps to
        `PricingRuleMatch`.
    *   `MatchRow` is a private record.
*   **Why**: Decision 6, with no cache (G2).

##### 39. `src/Goldfish.Infrastructure/Pricing/SqlPricingStatusReader.cs` (Create)

*   **Type / Scope**: `internal sealed` implementation of
    `IPricingStatusReader`.
*   **Proposed Changes**: One query returns the `VersionNumber`,
    `ChangeTicketRef`, and `PublishedOnUtc` of the version with the highest
    version number. It returns `NoActiveVersion` when no version exists.
*   **Why**: The active-version endpoint (G7).

##### 40. `src/Goldfish.Infrastructure/DependencyInjection.cs` (Modify)

*   **Type / Scope**: DI registration.
*   **Proposed Changes**: Register `IPolicyRepository`, `IQuoteRepository`,
    `IPricingRuleReader`, and `IPricingStatusReader`, all scoped.
*   **Why**: Connects the interfaces to their implementations.

#### Api

##### 41. `src/Goldfish.Api/Authentication/ApiKeyOptions.cs` (Create)

*   **Type / Scope**: Options classes.
*   **Proposed Changes**:
    *   `ApiKeyOptions { List<ApiKeyEntry> Keys }`.
    *   `ApiKeyEntry { string Name; string KeyHash; string[] Permissions }`.
    *   Bound from the `ApiKeys` configuration section.
    *   Validated on start: names are unique, each hash is 64 hex characters,
        and each permission is a known value.
*   **Why**: Decision 9. Only hashes are stored.

##### 42. `src/Goldfish.Api/Authentication/Permissions.cs` (Create)

*   **Type / Scope**: Constants and an extension method.
*   **Proposed Changes**:
    *   Constants: `policies:read`, `policies:write`, `quotes:create`,
        `quotes:read`, `pricing:read`.
    *   `AddPermissionPolicies(this AuthorizationBuilder)` registers one policy
        per permission, each requiring the claim `permission` with that value.
*   **Why**: The per-key permissions in the API table.

##### 43. `src/Goldfish.Api/Authentication/ApiKeyAuthenticationHandler.cs` (Create)

*   **Type / Scope**: `AuthenticationHandler<AuthenticationSchemeOptions>`,
    scheme `ApiKey`.
*   **Proposed Changes**:
    *   Reads the `X-Api-Key` header.
    *   If the header is missing, returns `NoResult`, which leads to a 401
        challenge.
    *   Hashes the value with SHA-256 and compares it against each entry with
        `CryptographicOperations.FixedTimeEquals`.
    *   On a match, issues a principal with a `Name` claim set to the entry
        name and one `permission` claim per permission.
    *   Otherwise returns `Fail`. The key value is never logged.
*   **Why**: Decision 9.

##### 44. `src/Goldfish.Api/Extensions/ResultExtensions.cs` (Create)

*   **Type / Scope**: Controller extension.
*   **Proposed Changes**: `IActionResult ToProblem(this ControllerBase c, Error error)`
    maps:
    *   Validation to 400;
    *   NotFound to 404;
    *   Unavailable to 503;
    *   Failure to 500.

    It uses `Problem(statusCode, title: error.Code, detail: error.Description)`.
*   **Why**: The single error mapping (Technical Plan, Error handling).

##### 45. `src/Goldfish.Api/Controllers/ProductsController.cs` (Modify)

*   **Type / Scope**: Controller.
*   **Proposed Changes**: Replace the two inline `Problem(...)` calls with
    `this.ToProblem(result.Error)`. Status codes are unchanged, and it stays
    anonymous.
*   **Why**: Uses the shared mapping.

##### 46. `src/Goldfish.Api/Controllers/PoliciesController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/policies")]`.
*   **Proposed Changes**:
    *   `POST` (`policies:write`) returns 201 with `CreatedAtAction(GetById)`.
    *   `GET {id:guid}` (`policies:read`).
    *   `PUT {id:guid}` (`policies:write`) returns 200.
    *   `POST {policyId:guid}/quotes` (`quotes:create`) passes
        `RequestedBy = User.Identity!.Name`, and returns 201 with
        `CreatedAtAction(nameof(QuotesController.GetById), "Quotes", ...)`.
    *   Request records `CreatePolicyRequest(string PolicyType, decimal CoverageLimit, decimal BasePrice)`
        and `UpdatePolicyRequest` (same fields) are declared in the file, as
        `CreateProductRequest` is today.
    *   Every action has `[Authorize(Policy = ...)]` and
        `ProducesResponseType` attributes.
*   **Why**: The policy and quote routes from the API table.

##### 47. `src/Goldfish.Api/Controllers/QuotesController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/quotes")]`.
*   **Proposed Changes**: `GET {id:guid}` (`quotes:read`).
*   **Why**: Reading a saved quote, and the target of the quote `POST`.

##### 48. `src/Goldfish.Api/Controllers/PricingController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/pricing")]`.
*   **Proposed Changes**: `GET active-version` (`pricing:read`) returns
    `ActiveVersionStatus`.
*   **Why**: The operator view (G7).

##### 49. `src/Goldfish.Api/Program.cs` (Modify)

*   **Type / Scope**: Composition root.
*   **Proposed Changes**:
    *   Add `services.AddOptions<ApiKeyOptions>().Bind(...).Validate(...).ValidateOnStart()`.
    *   Add `AddAuthentication("ApiKey").AddScheme<..., ApiKeyAuthenticationHandler>("ApiKey", null)`.
    *   Add `AddAuthorizationBuilder().AddPermissionPolicies()`.
    *   Call `app.UseAuthentication()` before `UseAuthorization()`.
    *   Add `public partial class Program;` at the end, for
        `WebApplicationFactory`.
*   **Why**: Turns on G8 and makes the API testable.

##### 50. `src/Goldfish.Api/appsettings.json` (Modify)

*   **Type / Scope**: Configuration.
*   **Proposed Changes**: Add `"ApiKeys": { "Keys": [] }`, with no keys
    committed.
*   **Why**: A safe default. With no keys configured, every protected endpoint
    returns 401.

##### 51. `src/Goldfish.Api/Goldfish.Api.csproj` (Modify)

*   **Type / Scope**: Build configuration.
*   **Proposed Changes**: Add a `UserSecretsId` so development key hashes can
    live in user secrets.
*   **Why**: Keeps development keys out of source control.

##### 52. `src/Goldfish.Api/Goldfish.Api.http` (Modify)

*   **Type / Scope**: Manual request samples.
*   **Proposed Changes**: Add sample requests for creating, getting, and
    updating a policy, creating and getting a quote, and the active-version
    endpoint. Each sends an `X-Api-Key: {{apiKey}}` variable.
*   **Why**: Manual verification of the CUJs.

#### Database scripts (outside migrations)

##### 53. `db/scripts/grant-permissions.sql` (Create)

*   **Type / Scope**: DBA-run T-SQL, using sqlcmd variables `$(ApiLogin)` and
    `$(PublisherLogin)`.
*   **Proposed Changes**:
    *   Creates the database users for the logins if they are missing.
    *   Grants the rights in the Technical Plan's permissions table.
    *   Explicitly `DENY UPDATE, DELETE` on the three pricing tables to both
        users.
    *   Can be run again safely.
*   **Why**: Decision 10. Logins differ per environment, so this is not part
    of the migrations.

##### 54. `db/scripts/verify-permissions.sql` (Create)

*   **Type / Scope**: Read-only T-SQL.
*   **Proposed Changes**: Lists the actual rights of both users on the pricing
    and policy objects from `sys.database_permissions`. Flags any UPDATE or
    DELETE right, and any membership in db_owner, db_datawriter, or
    db_ddladmin.
*   **Why**: Lets a reviewer confirm the no-edit guarantee in each
    environment.

##### 55. `db/templates/publish-rule-set-version.sql` (Create)

*   **Type / Scope**: Ticket script template.
*   **Proposed Changes**:
    *   `DECLARE @Rules pricing.PricingRuleInput;`.
    *   One `INSERT INTO @Rules` row per rule, with comments explaining each
        column.
    *   `EXEC pricing.PublishRuleSetVersion` with `@ChangeTicketRef`,
        `@ApprovedBy`, and `@Rules`. A comment states that the change is
        live the moment the script commits.
*   **Why**: Every ticket script has the same reviewable shape.

##### 56. `db/tickets/0001-initial-rule-set.sql` (Create)

*   **Type / Scope**: The first change-ticket script.
*   **Proposed Changes**: Publishes version 1 with two rules:
    *   "Large commercial" (priority 10, Commercial, more than 1,000,000,
        multiplier 1.5);
    *   "Default" (priority 1000, no conditions, multiplier 1.0).
*   **Why**: Rule data arrives through the ticket path, not migrations. This
    script shows the template in use.

#### Documentation

##### 57. `docs/pricing-rules.md` (Create)

*   **Type / Scope**: Runbook.
*   **Proposed Changes**:
    *   How rules match: priority, typed conditions and their boundaries, the
        catch-all, and rounding.
    *   How to write and review a ticket script, and the error each procedure
        check raises.
    *   That a version is live the moment its script commits, so a change due
        at a set time must have its script run at that time.
    *   How to correct a mistaken version by publishing a new one.
    *   How to generate an API key and its hash (PowerShell one-liner), and how
        to rotate it.
    *   The rollout order and the rule never to run the migration's `Down` in
        production.
    *   The accepted risks.
*   **Why**: Analysts, reviewers, and DBAs work outside the code and need one
    reference.

#### Tests

##### 58. `Goldfish.slnx` (Modify)

*   **Type / Scope**: Solution.
*   **Proposed Changes**: Add the two test projects under a `/tests/` folder.
*   **Why**: `dotnet test` runs everything.

##### 59. `tests/Goldfish.Domain.Tests/Goldfish.Domain.Tests.csproj` (Create)

*   **Type / Scope**: xUnit test project (`dotnet new xunit`, net10.0).
*   **Proposed Changes**: References `Goldfish.Domain`.
*   **Why**: Fast unit tests for domain rules.

##### 60. `tests/Goldfish.Domain.Tests/Policies/PolicyTests.cs` (Create)

*   **Type / Scope**: Unit tests.
*   **Proposed Changes**:
    *   Create and update succeed with valid values.
    *   They reject a coverage limit or base price of 0 or below, an undefined
        enum value, and a value with 3 decimal places.
*   **Why**: Policy validation.

##### 61. `tests/Goldfish.Domain.Tests/Quotes/QuoteTests.cs` (Create)

*   **Type / Scope**: Unit tests.
*   **Proposed Changes**:
    *   1,234.57 × 1.5 gives unrounded 1,851.855 and final 1,851.86.
    *   100.25 × 0.5 = 50.125 rounds to 50.12 (half to even, down).
    *   100.35 × 0.5 = 50.175 rounds to 50.18 (half to even, up).
    *   The snapshot fields are copied from the policy and the match.
*   **Why**: Decision 7 and G4.

##### 62. `tests/Goldfish.IntegrationTests/Goldfish.IntegrationTests.csproj` (Create)

*   **Type / Scope**: xUnit test project.
*   **Proposed Changes**:
    *   References `Goldfish.Api` and `Goldfish.Infrastructure`.
    *   Packages: `Microsoft.AspNetCore.Mvc.Testing` 10.0.x and
        `Microsoft.Data.SqlClient`, which comes through EF.
*   **Why**: Runs the edge cases against real SQL Server, which decision 6
    requires.

##### 63. `tests/Goldfish.IntegrationTests/Infrastructure/LocalDbFixture.cs` (Create)

*   **Type / Scope**: xUnit collection fixture.
*   **Proposed Changes**:
    *   Builds the connection string
        `Server=(localdb)\MSSQLLocalDB;Database=Goldfish_Test_<guid>;Trusted_Connection=True;TrustServerCertificate=True`.
    *   Applies the migrations with `Database.MigrateAsync()`.
    *   Drops the database on dispose.
    *   Provides a `ResetAsync()` that deletes test rows while running as the
        test user, who is sysadmin on LocalDB. Before deleting from the
        temporal table, it turns `SYSTEM_VERSIONING` off, then turns it back
        on afterward.
*   **Why**: Decision on test database (LocalDB). Each run uses an isolated
    database.

##### 64. `tests/Goldfish.IntegrationTests/Infrastructure/GoldfishApiFactory.cs` (Create)

*   **Type / Scope**: `WebApplicationFactory<Program>`.
*   **Proposed Changes**:
    *   Overrides `ConnectionStrings:Database` with the fixture's database.
    *   Overrides `ApiKeys:Keys` with three test keys:
        *   `full`, with all permissions;
        *   `quoter`, with `quotes:create` only;
        *   `reader`, with read permissions only.
    *   Provides a `CreateClient(string keyName)` helper that sets the
        `X-Api-Key` header.
*   **Why**: End-to-end HTTP tests with real authentication.

##### 65. `tests/Goldfish.IntegrationTests/Infrastructure/RuleSetPublisher.cs` (Create)

*   **Type / Scope**: Test helper.
*   **Proposed Changes**: Calls `pricing.PublishRuleSetVersion` through
    `SqlCommand`, passing a `DataTable` as a structured table-valued
    parameter.
*   **Why**: Tests publish rules the same way ticket scripts do.

##### 66. `tests/Goldfish.IntegrationTests/Pricing/PublishRuleSetVersionTests.cs` (Create)

*   **Type / Scope**: Integration tests.
*   **Proposed Changes**:
    *   Rejects:
        *   an empty rule set;
        *   no catch-all;
        *   two catch-alls;
        *   a catch-all that does not have the highest priority number;
        *   duplicate priorities;
        *   priority 0;
        *   a multiplier of 0;
        *   a minimum equal to or above the maximum;
        *   an unknown policy type;
        *   a duplicate ticket reference.
    *   After each rejection, the version count is unchanged.
    *   A valid publish assigns the next version number.
*   **Why**: CUJ 4, G6, and decision 12.

##### 67. `tests/Goldfish.IntegrationTests/Pricing/PricingRuleReaderTests.cs` (Create)

*   **Type / Scope**: Integration tests.
*   **Proposed Changes**:
    *   With no version, the result is `NoActiveVersion`.
    *   A coverage limit of exactly 1,000,000 falls through to the catch-all.
    *   A limit of 1,000,000.01 matches "Large commercial".
    *   The lower priority number wins when two rules match.
    *   A `Personal` policy never matches a `Commercial` rule.
    *   A newly published version is used on the very next call.
    *   With versions 1 and 2 published, version 2 is always the one matched.
*   **Why**: Decisions 2–6, G2, and G3.

##### 68. `tests/Goldfish.IntegrationTests/Api/QuoteEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   CUJ 1 returns 201, and the saved row's values match the response.
    *   CUJ 2 returns 201 with `WasCatchAll = true`.
    *   No key returns 401. The `reader` key returns 403.
    *   An unknown policy returns 404.
    *   No active version returns 503.
    *   A saved quote keeps its version after a new version is published.
        `GET /api/quotes/{id}` returns 200 for the `reader` key.
*   **Why**: CUJs 1–3, G4, and G8.

##### 69. `tests/Goldfish.IntegrationTests/Api/PolicyEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   Create returns 201.
    *   A lowercase `commercial` or a numeric `"0"` type returns 400.
    *   Update returns 200, and `dbo.PoliciesHistory` holds the old row.
    *   Update of an unknown id returns 404.
    *   The `quoter` key returns 403 on `PUT`.
*   **Why**: CUJ 6 and decision 11.

##### 70. `tests/Goldfish.IntegrationTests/Api/ActiveVersionEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   Shows the latest version's number and ticket reference, and changes
        after a new version is published.
    *   Returns 503 when no version exists.
*   **Why**: CUJ 5 and G7.

##### 71. `tests/Goldfish.IntegrationTests/Database/PermissionTests.cs` (Create)

*   **Type / Scope**: Integration tests.
*   **Proposed Changes**:
    *   Creates the users `api_test` and `publisher_test` `WITHOUT LOGIN`, and
        runs `db/scripts/grant-permissions.sql` against them.
    *   Using `EXECUTE AS USER`, checks that:
        *   `UPDATE` and `DELETE` on all three pricing tables fail for both
            users;
        *   a direct `INSERT` into `pricing.PricingRules` fails for
            `publisher_test`;
        *   `EXEC pricing.PublishRuleSetVersion` succeeds for
            `publisher_test`;
        *   the API user can insert quotes.
*   **Why**: G5 and decision 10. The grant script is tested, not just written.

### Verification Plan

*   **Compilation/Build**: `dotnet build Goldfish.slnx` finishes with 0
    warnings and 0 errors.
*   **Migrations**:
    *   `dotnet ef migrations has-pending-model-changes -p src/Goldfish.Infrastructure -s src/Goldfish.Api`
        reports no changes.
    *   `dotnet ef database update` applies cleanly to an empty LocalDB
        database.
*   **Unit Tests**: `dotnet test tests/Goldfish.Domain.Tests`.
*   **Integration Tests**: `dotnet test tests/Goldfish.IntegrationTests`. It
    needs Windows with LocalDB `MSSQLLocalDB`, so CI agents must be Windows.
*   **Static Analysis & Linting**: the build runs with
    `TreatWarningsAsErrors` for the new test projects. Nullable reference
    types stay enabled in all projects.
*   **Test Coverage Requirement**: every CUJ and every check in
    `pricing.PublishRuleSetVersion` has at least one test. The domain
    projects have 90% or more line coverage of `Policy` and `Quote`.
*   **Manual**:
    *   Run the API.
    *   Run `db/tickets/0001-initial-rule-set.sql` against LocalDB.
    *   Exercise `Goldfish.Api.http` with a development key from user secrets.
    *   Confirm the CUJ 1 numbers.

--------------------------------------------------------------------------------

<!-- Provenance: Indicates initial structural drafting via EGM. -->

*Drafted with EGM*
