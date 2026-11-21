# Policy Pricing Rules

*   **Author:** Do-Not-Be-Evil
*   **Status:** Proposed
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
    the matching rule with the lowest priority number sets the price. Rules are
    grouped into versions. A version is never changed after it is published; a
    correction is a new version.
3.  **Quotes.** A new endpoint takes a policy, finds the winning rule in the
    current version, multiplies the base price, and saves a quote record. The
    record holds every value the price came from, so the price can be explained
    later even after the rules or the policy change.

Example rule: if the policy type is Commercial and the coverage limit is over
$1,000,000, the price is 1.5 times the base price.

The expected callers are internal systems (assumed, for example an
underwriting portal), each with its own API key. People do not call the API
directly.

--------------------------------------------------------------------------------

## The Problem

If pricing lives in C# code, every monthly change needs a developer, a build,
and a deployment. That puts engineering in the path of a business decision and
adds a release cycle to each change.

Moving the rules into the database removes that dependency. It also creates
three risks that the design has to control:

*   **Mistakes reach customers directly.** A wrong multiplier in a script, such
    as 15 instead of 1.5, prices every matching quote wrongly as soon as the
    script runs. No compiler or code review catches it. The change-ticket
    review is the only human check, so the database must reject values outside
    the limits listed in G6, and the reviewer must be able to see exactly what
    a script changes before it runs.
*   **Prices must be explainable.** A customer, auditor, or regulator may ask
    why a policy was priced at a given amount months after the fact. By then the
    rules and the policy's base price may have changed. If the system only
    stores the final number, the question cannot be answered.
*   **Stale rules are not acceptable.** Once a change is approved and its script
    commits, the next quote must use it. A delay of even a few seconds is
    unacceptable to the business. That rules out holding rules in memory and
    refreshing them on a timer.

Goldfish has no pricing support today. Its only data is a sample `Products`
table. There are no policy tables, no pricing logic, no tests, no logging, and
no authentication.

--------------------------------------------------------------------------------

## Goals

*   **G1. Rule changes need no deployment.** A pricing change made through a
    change-ticket script applies to quotes without a code change, build,
    deployment, or restart.
*   **G2. No stale pricing.** The first quote request that starts after a rule
    change commits uses the new rules. No request ever sees part of a change,
    and no publish can silently undo another approved change.
*   **G3. Same input, same price.** For a given policy and rule version, the
    winning rule and the final price are always the same. Ties between rules
    cannot happen, and the behavior when no rule matches is defined.
*   **G4. Every quote can be explained.** Each saved quote records the rule
    version, the rule that matched, the policy values that were evaluated, the
    multiplier, and the final price. Explaining a past quote needs only the
    quote row and the immutable rule rows it references.
*   **G5. Rule history cannot be rewritten through normal access.** No login
    used by the API or by ticket scripts can update or delete published rules
    or quotes, and SQL Server enforces this with permissions. Each version
    records the change ticket that approved it. Logins with db_owner, sysadmin,
    or schema-change rights are an accepted risk (see Risks & Mitigations).
*   **G6. Invalid rules are rejected when the script runs.** The rejected
    conditions are listed in the procedure's error table: for example a
    multiplier of zero or below, duplicate priorities in one version, a minimum
    not below the maximum, or a missing catch-all. They never reach a live
    quote. A dry run shows each multiplier change against the live version
    before publishing.
*   **G7. Pricing decisions can be investigated.** Operators can see which rule
    version the API currently treats as active, and each pricing decision is
    logged with the fields listed under Logging and visibility.
*   **G8. The API is not anonymous.** Every endpoint except the sample Products
    endpoints requires an authenticated caller.
*   **G9. Policy changes are traceable.** Every policy create and update records
    which caller made it, SQL Server keeps every earlier version of the row, and
    a stale update cannot silently overwrite a newer one.

--------------------------------------------------------------------------------

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
*   **Date-tied pricing changes.** No pricing change is tied to a date or time.
    A change applies when its approved script runs.
*   **Rate tables for base price.** The base price is stored on each policy. It
    is not looked up from a table of rates by state, industry, or similar.
*   **Multiple currencies.** All amounts are in one currency.
*   **Re-pricing past quotes.** A rule change affects new quotes only. Saved
    quotes keep the price they were given.
*   **Delivering rule data through EF Core migrations.** Migrations change table
    structure only. Rule rows arrive only through change-ticket scripts.

--------------------------------------------------------------------------------

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

--------------------------------------------------------------------------------

## Critical User Journeys (CUJs)

### CUJ 1: Quote a commercial policy over the coverage threshold

*   **Given**: Rule version 1 is active. It contains rule "Large commercial"
    (priority 10, PolicyType = Commercial, MinCoverageLimitExclusive =
    1,000,000, multiplier 1.5) and a catch-all rule "Default" (priority 1000,
    no conditions, multiplier 1.0). Policy P has type Commercial, coverage limit
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
    quote and its log entry have `WasCatchAll = true`.

### CUJ 3: Analysts publish a change that applies immediately

*   **Given**: A change ticket raising "Large commercial" to 1.6 is approved.
    The ticket includes the dry-run output, which shows "Large commercial"
    changing from 1.5 to 1.6 (ratio 1.0667) and "Default" unchanged.
*   **When**: The ticket script calls `pricing.PublishRuleSetVersion` with
    `@BaseVersionNumber = 1` and the full rule set, including the catch-all.
*   **Then**: The procedure validates the set and inserts version 2 in one
    transaction. The first quote request that starts after the commit uses
    version 2. Quotes already saved still show version 1.

### CUJ 4: An invalid version is rejected before it goes live

*   **Given**: A ticket script submits a rule set with no catch-all rule, or
    with two rules at priority 10, or with a multiplier of 0.
*   **When**: The script calls `pricing.PublishRuleSetVersion`.
*   **Then**: The procedure raises the error listed for that condition in the
    procedure's error table, and nothing is inserted. Version 1 stays active.

### CUJ 5: A mistaken version is corrected

*   **Given**: Version 2 went live with "Large commercial" at 1.6, but the
    approved value was 1.06.
*   **When**: A new change ticket publishes version 3 with
    `@BaseVersionNumber = 2`, a full copy of the rule set with 1.06.
*   **Then**: The next quote uses version 3. Version 2 and the quotes priced by
    it stay unchanged in the database, so the mistake and its impact can be
    traced. The active-version endpoint shows version 3.

### CUJ 6: A base-price edit is recorded

*   **Given**: Policy P has base price 1,234.57, was quoted yesterday, and its
    current ETag is E1.
*   **When**: A caller with `policies:write` sends `PUT /api/policies/{P}` with
    base price 1,300.00 and `If-Match: E1`.
*   **Then**: The API returns 200 with a new ETag E2. The policy row shows
    1,300.00 and `ModifiedBy` set to the caller's key name. SQL Server's history
    table keeps the earlier row, with its own `ModifiedBy` and validity period.
    Yesterday's quote still shows 1,234.57 as the base price it used.
*   **Then**: A second `PUT` that still sends `If-Match: E1` returns 412
    `Policy.Concurrency`, and the row is unchanged.

### CUJ 7: A stale script is rejected

*   **Given**: Version 2 exists. A ticket script was prepared from version 1.
*   **When**: The script runs with `@BaseVersionNumber = 1`.
*   **Then**: The procedure raises error 50003 ("base version is not the latest;
    rebuild the script from version 2"), and nothing is inserted.

### CUJ 8: A caller without a valid key is refused

*   **Given**: Any state.
*   **When**: A request to any policy, quote, or pricing endpoint has no
    `X-Api-Key` header, or an unknown key.
*   **Then**: The API returns 401, and nothing is read or saved.

### CUJ 9: A quote before the first publish

*   **Given**: No rule version has been published.
*   **When**: The caller requests a quote.
*   **Then**: The API returns 503 `Pricing.NoActiveVersion`. No quote is priced
    or saved, and the error code names the cause.

--------------------------------------------------------------------------------

## Technical Plan (Architecture)

### Decisions

| # | Topic | Decision |
| --- | --- | --- |
| 1 | What is a version? | A full copy of the rule set under one header row. |
| 2 | Which priority wins? | The lowest priority number. Priority must be 1 or more. |
| 3 | What if no rule matches? | Every version must contain exactly one catch-all rule, with the largest priority number in that version. |
| 4 | How is the active version chosen? | The newest committed version (highest `VersionNumber`). There are no effective dates and no clock comparison, because no pricing change is tied to a time. A change applies as soon as it commits. |
| 5 | How are conditions stored? | Typed columns: `PolicyType`, `MinCoverageLimitExclusive`, `MaxCoverageLimitInclusive`. |
| 6 | Where is the winner picked? | In one SQL query. |
| 7 | How is the final price rounded? | 2 decimal places, `MidpointRounding.ToEven` (banker's rounding), written explicitly. The unrounded value is also stored. |
| 8 | Which multipliers are allowed? | Greater than 0, no ceiling beyond the column type (`decimal(9,4)`, at most 99,999.9999). |
| 9 | How do callers authenticate? | API keys in an `X-Api-Key` header, checked against stored SHA-256 hashes. Each key has named permissions. A fallback authorization policy covers every endpoint. |
| 10 | How are edits prevented? | Database permissions. No login used by the API or by ticket scripts has UPDATE or DELETE on pricing tables. |
| 11 | How do policies change? | Create and update endpoints. `Policies` is a SQL Server temporal table with `ModifiedBy` and a `rowversion` checked through `If-Match`. |
| 12 | How is a version published? | A stored procedure, `pricing.PublishRuleSetVersion`, validates and inserts in one transaction. It requires the base version number and supports a dry run. |
| 13 | Which database do tests use? | LocalDB, because matching runs in SQL (decision 6) and needs a real SQL Server to test. |

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
                                  │ EF Core repositories, SqlPricingRuleReader (one SQL query)
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
created with `Guid.CreateVersion7()`, except where noted. Every `PolicyType`
column uses the binary collation `Latin1_General_BIN2`, so comparisons and
CHECK constraints are case-sensitive, like C#.

**`dbo.Policies`** (temporal; history in `dbo.PoliciesHistory`)

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key |
| PolicyType | nvarchar(32) | `CK_Policies_PolicyType`: in (`Commercial`, `Personal`) |
| CoverageLimit | decimal(18,2) | `CK_Policies_CoverageLimit_Positive`: > 0 |
| BasePrice | decimal(18,2) | `CK_Policies_BasePrice_Positive`: > 0 |
| CreatedOnUtc | datetime2 | Set by the app from `TimeProvider` |
| ModifiedBy | nvarchar(64) | The API key name of the caller that created or last updated the row |
| RowVersion | rowversion | Concurrency token, sent to callers as the ETag |
| PeriodStart | datetime2 | Managed by SQL Server for history |
| PeriodEnd | datetime2 | Managed by SQL Server for history |

**`pricing.PricingRuleSetVersions`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key, default `NEWSEQUENTIALID()` |
| VersionNumber | int | `UX_PricingRuleSetVersions_VersionNumber`. Set by the procedure to `@BaseVersionNumber + 1`. |
| ChangeTicketRef | nvarchar(64) | Required. `UX_PricingRuleSetVersions_ChangeTicketRef`. |
| ApprovedBy | nvarchar(128) | Required |
| PublishedOnUtc | datetime2 | Default `SYSUTCDATETIME()`. For the record only; never used to choose a version. |

The unique index on `VersionNumber` also serves the active-version lookup, and
it is what rejects two publishes from the same base.

**`pricing.PricingRules`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key, default `NEWSEQUENTIALID()` |
| VersionId | uniqueidentifier | Foreign key `FK_PricingRules_Versions` |
| Priority | int | `CK_PricingRules_Priority_Min`: >= 1. `UX_PricingRules_Version_Priority`: unique with VersionId. |
| Name | nvarchar(200) | Required. `UX_PricingRules_Version_Name`: unique with VersionId, so a dry run can match rules across versions. |
| PolicyType | nvarchar(32) NULL | NULL means any type. Otherwise `CK_PricingRules_PolicyType`: in the policy type list. |
| MinCoverageLimitExclusive | decimal(18,2) NULL | NULL means no lower limit |
| MaxCoverageLimitInclusive | decimal(18,2) NULL | NULL means no upper limit. `CK_PricingRules_CoverageRange`: Min < Max when both are set. |
| Multiplier | decimal(9,4) | `CK_PricingRules_Multiplier_Positive`: > 0 |

Indexes on PricingRules:

*   `UX_PricingRules_Version_CatchAll` is a filtered unique index on `VersionId`
    where all three condition columns are NULL. It allows at most one catch-all
    per version. The procedure enforces "exactly one" and "largest priority
    number".
*   `UX_PricingRules_Version_Id` is a unique index on (`VersionId`, `Id`). It
    lets each quote's rule be tied to that quote's version by a composite
    foreign key.

**`pricing.Quotes`**

| Column | Type | Rules |
| --- | --- | --- |
| Id | uniqueidentifier | Primary key |
| PolicyId | uniqueidentifier | Foreign key to policies |
| VersionId | uniqueidentifier | Foreign key to versions |
| VersionNumber | int | Copy, for reading without a join |
| RuleId | uniqueidentifier | Composite foreign key (`VersionId`, `RuleId`) to rules, so the rule must belong to the quote's version |
| RuleName | nvarchar(200) | Copy |
| WasCatchAll | bit | True when the catch-all priced the quote |
| PolicyType | nvarchar(32) | Copy of the value evaluated. `CK_Quotes_PolicyType`. |
| CoverageLimit | decimal(18,2) | Copy of the value evaluated |
| BasePrice | decimal(18,2) | Copy of the value evaluated |
| Multiplier | decimal(9,4) | Copy |
| UnroundedPrice | decimal(28,6) | BasePrice × Multiplier |
| FinalPrice | decimal(18,2) | UnroundedPrice rounded to 2 places, ToEven |
| RequestedBy | nvarchar(64) | The API key's name |
| CreatedOnUtc | datetime2 | From `TimeProvider` |

The copies of VersionNumber, RuleName, and Multiplier are kept on purpose, so a
quote stays readable even if a privileged login alters rule rows (see Risks &
Mitigations). `CreatedOnUtc` and `PublishedOnUtc` are for the record only.
They come from different clocks and are never compared.

Indexes on Quotes: `PolicyId`. At the assumed tens of quotes per second, Quotes
grows by roughly 30–300 million rows a year (about 10–90 GB before indexes).
Archiving is deferred until retention is decided.

### Choosing the active version

The active version is the committed version with the highest version number:
`SELECT TOP (1) ... ORDER BY VersionNumber DESC`. No clock is compared, so
differences between server clocks cannot delay a change. A version becomes
active at the moment its publishing transaction commits.

No pricing change is tied to a date or time. A change applies when its approved
script runs.

### Finding the winning rule

`SqlPricingRuleReader` runs one parameterized query through
`Database.SqlQuery<MatchRow>` with a `FormattableString`, so `{policyType}` and
`{coverageLimit}` are sent as parameters:

```sql
WITH active AS (
    SELECT TOP (1) Id, VersionNumber
    FROM pricing.PricingRuleSetVersions
    ORDER BY VersionNumber DESC
)
SELECT a.Id AS VersionId, a.VersionNumber,
       m.Id AS RuleId, m.Name, m.Priority, m.Multiplier, m.WasCatchAll
FROM active a
OUTER APPLY (
    SELECT TOP (1) r.Id, r.Name, r.Priority, r.Multiplier,
           CAST(CASE WHEN r.PolicyType IS NULL
                      AND r.MinCoverageLimitExclusive IS NULL
                      AND r.MaxCoverageLimitInclusive IS NULL
                     THEN 1 ELSE 0 END AS bit) AS WasCatchAll
    FROM pricing.PricingRules r
    WHERE r.VersionId = a.Id
      AND (r.PolicyType IS NULL OR r.PolicyType = {policyType})
      AND (r.MinCoverageLimitExclusive IS NULL OR {coverageLimit} > r.MinCoverageLimitExclusive)
      AND (r.MaxCoverageLimitInclusive IS NULL OR {coverageLimit} <= r.MaxCoverageLimitInclusive)
    ORDER BY r.Priority ASC
) m;
```

The query has three outcomes:

*   **No row.** No version has been published yet. The result is
    `Pricing.NoActiveVersion`, returned as 503.
*   **A row with a null RuleId.** The active version has no matching rule.
    This can happen only if rule rows were written outside
    `pricing.PublishRuleSetVersion`, for example by a privileged login. The
    result is `Pricing.InvalidActiveVersion`, returned as 500 and logged as an
    error.
*   **A row with a rule.** This is the match.

Matching runs only in SQL. Both `PolicyType` columns use the binary collation
`Latin1_General_BIN2`, so the CHECK constraints reject `commercial`, and the
SQL comparison is case-sensitive like C#'s. The C# side accepts only the exact
names (see `PolicyTypeParser`).

The rules are read on every request with no cache. The query and the quote
insert are separate statements. If a new version commits between them, the
quote's VersionId and RuleId still identify the rule that priced it.

### Publishing a version

`pricing.PublishRuleSetVersion` is created by an EF Core migration using raw
SQL. It is the only way rule rows are written.

**Procedure contract**

*   Table type `pricing.PricingRuleInput`, with columns in this order:
    *   `Priority int NOT NULL`
    *   `Name nvarchar(200) NOT NULL`
    *   `PolicyType nvarchar(32) COLLATE Latin1_General_BIN2 NULL`
    *   `MinCoverageLimitExclusive decimal(18,2) NULL`
    *   `MaxCoverageLimitInclusive decimal(18,2) NULL`
    *   `Multiplier decimal(9,4) NOT NULL`
*   Parameters:
    *   `@ChangeTicketRef nvarchar(64)`
    *   `@ApprovedBy nvarchar(128)`
    *   `@BaseVersionNumber int`: the version the script was built from, or 0
        when no version exists yet
    *   `@DryRun bit = 0`
    *   `@Rules pricing.PricingRuleInput READONLY`
    *   `@NewVersionNumber int OUTPUT`

**Steps** (one transaction, `SET XACT_ABORT ON`, so any error rolls back
everything)

1.  Rejects a blank `@ChangeTicketRef` or `@ApprovedBy` (50001).
2.  Rejects an empty rule list (50002).
3.  Rejects the call unless `@BaseVersionNumber` equals the current highest
    version number, or 0 when no version exists (50003). The new version number
    is `@BaseVersionNumber + 1`. If two publishes from the same base run at
    once, the second violates the unique index on `VersionNumber` and is rolled
    back.
4.  Rejects the call unless exactly one rule has all three conditions NULL
    (50004), and unless that rule's priority number is larger than every other
    rule's (50005).
5.  Inserts the header and all rules. The named CHECK constraints and unique
    indexes reject the remaining invalid values (see the error table).
6.  When `@DryRun = 1`, returns one row per rule and then rolls back. Each row
    has Name, OldMultiplier, NewMultiplier, Ratio, and Change (`Added`,
    `Removed`, `Changed`, or `Unchanged`). Rules are matched to the base version
    by `Name`. The ticket reviewer attaches this output to the ticket.
7.  Otherwise commits, and sets `@NewVersionNumber`.

**Error table**

| Rejected condition | Enforced by | Error |
| --- | --- | --- |
| Blank ticket reference or approver | THROW | 50001 |
| Empty rule set | THROW | 50002 |
| Stale base version | THROW | 50003 |
| Not exactly one catch-all rule | THROW | 50004 |
| Catch-all does not have the largest priority number | THROW | 50005 |
| Two publishes from the same base at once | `UX_PricingRuleSetVersions_VersionNumber` | 2601 |
| Priority below 1 | `CK_PricingRules_Priority_Min` | 547 |
| Multiplier not above 0 | `CK_PricingRules_Multiplier_Positive` | 547 |
| Minimum not below maximum | `CK_PricingRules_CoverageRange` | 547 |
| Unknown policy type | `CK_PricingRules_PolicyType` | 547 |
| Duplicate priority | `UX_PricingRules_Version_Priority` | 2601 |
| Duplicate rule name | `UX_PricingRules_Version_Name` | 2601 |
| Duplicate ticket reference | `UX_PricingRuleSetVersions_ChangeTicketRef` | 2601 |

SQL Server's error for a constraint names the constraint, so each name above
describes the problem.

**Reads during a publish.** Rules are only ever inserted, and each version is
inserted in one transaction. So under SQL Server's default READ COMMITTED
isolation, a quote reads either the whole previous version or the whole new
one. At worst a quote waits a few milliseconds for a publish to commit.

### Database permissions

Permissions are applied by a reviewed script, `db/scripts/grant-permissions.sql`,
which a DBA runs. It is not part of the migrations, because logins are managed
per environment. The `pricing` schema and all its objects are owned by `dbo`,
which ownership chaining requires.

| Login | Rights |
| --- | --- |
| API (`goldfish_api`) | SELECT on `pricing.PricingRuleSetVersions` and `pricing.PricingRules`. SELECT and INSERT on `pricing.Quotes`. SELECT, INSERT and UPDATE on `dbo.Policies`. SELECT on `dbo.PoliciesHistory`. SELECT and INSERT on `dbo.Products`. |
| Ticket scripts (`goldfish_pricing_publisher`) | EXECUTE on `pricing.PublishRuleSetVersion`. EXECUTE and REFERENCES on `TYPE::pricing.PricingRuleInput`, needed to declare the table-valued parameter. Ownership chaining lets the procedure insert rows the login cannot insert directly. |
| Migrations (`goldfish_migrator`) | Schema changes. Used only by deployments. |

A companion script, `db/scripts/verify-permissions.sql`, lists the actual
grants so a reviewer can check them. For what these permissions do not cover,
see Risks & Mitigations.

### API

| Method and route | Permission | Success | Errors |
| --- | --- | --- | --- |
| `POST /api/policies` | `policies:write` | 201 + PolicyResponse, `ETag` header | 400 `Policy.Invalid*`; 401; 403 |
| `GET /api/policies/{id}` | `policies:read` | 200 + PolicyResponse, `ETag` header | 404 `Policy.NotFound`; 401; 403 |
| `PUT /api/policies/{id}` | `policies:write` | 200 + PolicyResponse, new `ETag` header | 400 `Policy.Invalid*`; 404 `Policy.NotFound`; 412 `Policy.Concurrency`; 428 (no `If-Match`); 401; 403 |
| `POST /api/policies/{policyId}/quotes` | `quotes:create` | 201 + QuoteResponse | 404 `Policy.NotFound`; 503 `Pricing.NoActiveVersion`; 500 `Pricing.InvalidActiveVersion`; 401; 403 |
| `GET /api/quotes/{id}` | `quotes:read` | 200 + QuoteResponse | 404 `Quote.NotFound`; 401; 403 |
| `GET /api/pricing/active-version` | `pricing:read` | 200: active version number, its change ticket reference, and when it was published | 503 `Pricing.NoActiveVersion`; 401; 403 |

401 means a missing or unknown key. 403 means the key lacks the required
permission.

The ETag is the policy's `RowVersion`, base64-encoded and quoted. `PUT` requires
`If-Match` with the current ETag.

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

`Error` gains an `ErrorType` (`Validation`, `NotFound`, `PreconditionFailed`,
`Unavailable`, `Failure`). A single Api extension maps `ErrorType` to the HTTP
status codes in the API table (400, 404, 412, 503, 500) and builds
ProblemDetails, so controllers no longer switch on code strings. Existing
`ProductErrors` become `Validation` and `NotFound`. The 428 for a missing
`If-Match` header is an HTTP concern, returned by the controller.

### API key authentication

Keys are configured under `ApiKeys:Keys` as a list of entries, each with:

*   `Name` (1–64 characters, recorded as `RequestedBy` and `ModifiedBy`)
*   `KeyHash` (hex SHA-256 of the key)
*   `Permissions` (a list of permission strings)

Only hashes are stored. Development hashes go in user secrets, and production
hashes come from environment configuration. Keys are 32 random bytes, encoded
as base64url, so an unsalted SHA-256 hash is safe to store. Keys are sent only
over HTTPS. Configuration is read at startup, so rotating a key (add the new
entry, move callers, remove the old entry) needs a restart but no code change.

`ApiKeyAuthenticationHandler` hashes the incoming header value and compares it
in constant time with each stored hash. On a match it creates an authenticated
identity with the key's name and one `permission` claim per permission. Each
permission is an authorization policy, applied with `[Authorize(Policy = ...)]`.
A fallback authorization policy requires an authenticated caller on every
endpoint that does not say otherwise.

### Logging and visibility (G7)

`CreateQuoteHandler` writes one structured log entry per quote, at Information
level, with:

*   QuoteId
*   PolicyId
*   VersionNumber
*   RuleId
*   WasCatchAll
*   Multiplier
*   FinalPrice
*   RequestedBy

Quotes priced by the catch-all are logged at Information with
`WasCatchAll = true`. Operators monitor the catch-all rate and per-version
volume with a query over `pricing.Quotes`, grouped by VersionNumber and
WasCatchAll, documented in the runbook. Failed quote requests are logged by
error code (see file 21). The `/api/pricing/active-version` endpoint shows
operators which version is active and which change ticket published it.

--------------------------------------------------------------------------------

## Alternatives Considered

"Rejected in review" means rejected by the multi-reviewer analysis recorded in
the [concept brief](../../concepts_brief.md). "Rejected by author" means the
author chose against it while drafting this design.

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

*   **Pros**: A forward-chaining rules engine with rules written as compiled C#
    classes.
*   **Cons**:
    *   Because rules are code, every change needs a deployment.
    *   Loading rules from the database would mean building rule models at
        runtime.
*   **Reason for Rejection**: Rejected in review. In its normal use it fails G1,
    and its chaining model does far more than picking one winning rule needs.

### Alternative D: Rule conditions in a JSON column

*   **Pros**: New condition fields need no schema change.
*   **Cons**:
    *   CHECK constraints can only validate the contents through `ISJSON` and
        `JSON_VALUE`, which are hard to review.
    *   It is harder to query for audit.
    *   Ticket reviewers read JSON instead of columns.
*   **Reason for Rejection**: Rejected in review. It weakens G6.

### Alternative E: An in-memory rule cache with push invalidation (SqlDependency or Service Broker)

*   **Pros**: Fewer database reads per quote.
*   **Cons**: It adds a messaging channel that can fail silently and leave
    servers on stale rules. A cache that refreshes on a timer breaks G2
    outright.
*   **Reason for Rejection**: Rejected in review. One indexed query per quote
    is cheap at the assumed load. If a cache is ever needed, a cache keyed by
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
    version id reproduces any quote. The dry-run diff gives reviewers the
    "exactly what changed" view without per-rule versions.

### Alternative G: Versions that hold only the changes since the last version

*   **Pros**: Each publish is applied all at once, and scripts stay small.
*   **Cons**:
    *   The active rule set must be worked out from the whole history on every
        quote.
    *   Preventing ties needs trigger logic.
    *   A long history eventually needs a stored snapshot, which ends up much
        like a full copy anyway.
*   **Reason for Rejection**: Rejected by author, in favor of full copies of the
    rule set. Full copies keep each quote reproducible from one version id.

### Alternative H: The highest priority number wins

*   **Pros**: There is always room above the current top rule.
*   **Cons**: Anyone used to "priority 1 means most important" would write
    rules backwards, and no database check catches that.
*   **Reason for Rejection**: Rejected by author. The lowest number wins
    (decision 2), which matches how analysts already say "priority 1".

### Alternative I: No match returns an error, or the catch-all is optional

*   **Pros**: Gaps in the rules show up straight away as failed requests.
*   **Cons**:
    *   A script that narrows a condition becomes an outage for every policy it
        no longer covers.
    *   An optional catch-all makes behavior differ between versions.
*   **Reason for Rejection**: Rejected by author. Every version must contain
    exactly one catch-all (decision 3). Quotes priced by the catch-all are
    flagged by `WasCatchAll` and the runbook query, and the dry-run diff shows
    when a rule's coverage changes.

### Alternative J: Future-dated versions, with SQL Server's clock deciding

*   **Pros**:
    *   A ticket script can run days early and switch on at an exact time.
    *   One clock serves every API server.
*   **Cons**:
    *   It adds an effective-date column, a clock comparison in every quote
        query, and a rule that effective times must not go backwards.
    *   Operators need a view of versions that are scheduled but not yet active.
*   **Reason for Rejection**: Rejected by author. No pricing change is tied to a
    date or time, so effective dates would add a column, a clock comparison,
    and a scheduled-versions view for no current need. If one appears, an
    approved script can be run as a scheduled job without schema changes.

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
*   **Reason for Rejection**: Rejected by author, in favor of typed columns
    (decision 5). Two condition fields don't justify a generic condition model.

### Alternative M: Pick the winning rule in C#

*   **Pros**: Every matching edge case can be unit-tested without a database,
    and text comparison follows one set of C# rules.
*   **Cons**: Every quote loads every rule in the active version, and the
    definition of the catch-all would live in both C# and SQL.
*   **Reason for Rejection**: Rejected by author. Matching in SQL keeps the
    precedence logic next to the constraints it relies on, in one query
    (decision 6). The cost is that edge cases are tested only against LocalDB,
    so CI must run on Windows.

### Alternative N: Round halves up, or store the exact price

*   **Pros**: Rounding halves up matches what spreadsheets do. Storing the exact
    price loses nothing.
*   **Cons**:
    *   Rounding halves up creeps slightly upward over many quotes, and it is
        not the .NET default.
    *   Storing the exact price forces every system that reads it to round the
        same way.
*   **Reason for Rejection**: Rejected by author, in favor of banker's rounding
    to 2 decimal places (decision 7). The unrounded value is also stored, so a
    one-cent difference can be explained.

### Alternative O: A ceiling on multipliers, or surcharges only

*   **Pros**:
    *   A ceiling would make the database reject an extra digit, such as 15
        instead of 1.5.
    *   Allowing surcharges only would also reject a misplaced decimal point,
        such as 0.15.
*   **Cons**: A real rule above the ceiling, or any discount, would need a
    migration.
*   **Reason for Rejection**: Rejected by author. A multiplier only has to be
    above 0 (decision 8). The dry-run diff shows the ratio of every multiplier
    change to the ticket reviewer instead.

### Alternative P: JWT bearer tokens from an identity provider

*   **Pros**: Each calling system, and each person, has its own identity, and
    the API stores no secrets.
*   **Cons**: It needs an app registration with an identity provider, and the
    integration tests need a test token issuer.
*   **Reason for Rejection**: Rejected by author, in favor of API keys
    (decision 9). API keys were kept because no identity provider has been
    confirmed for these callers. The accepted cost is that keys identify
    calling systems, not people, and rotation is manual.

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
    endpoints with a temporal table, `ModifiedBy`, and `If-Match` (decision 11).

### Alternative S: Ticket scripts insert rows directly from a reviewed template

*   **Pros**: Scripts are plain INSERT statements, with no stored procedure.
*   **Cons**:
    *   Two things depend on people following the template: that the whole
        change is applied at once, and that the version has exactly one
        catch-all rule.
    *   A broken version shows up only as failed quotes.
*   **Reason for Rejection**: Rejected by author. It conflicts with G2 and G6.
    Publishing goes through `pricing.PublishRuleSetVersion` (decision 12).

### Alternative T1: A fixed base price for every policy

*   **Pros**: The simplest option: a fixed $100,000 base for every policy.
*   **Cons**: The base price varies by policy.
*   **Reason for Rejection**: Rejected by author. Base prices differ per policy.

### Alternative T2: The base price sent with each quote request

*   **Pros**: No storage is needed.
*   **Cons**: The caller would decide the base price, and a quote could not be
    tied to a stored policy.
*   **Reason for Rejection**: Rejected by author. The base price must come from
    data stored in this system.

### Alternative T3: Base prices read from an existing outside system

*   **Pros**: Reuses existing data.
*   **Cons**: The API would depend on another system's schema and availability.
*   **Reason for Rejection**: Rejected by author. Base prices are kept in new
    Goldfish tables.

### Alternative U: A rule-editing screen and an in-app approval workflow

*   **Pros**: Analysts could edit rules without writing SQL.
*   **Cons**: It is a separate application with its own screens, permissions,
    and approval states, and it duplicates the existing change-ticket review.
*   **Reason for Rejection**: Rejected by author. Review already happens in the
    change-ticket system.

### Alternative V: Two roles instead of five permissions

*   **Pros**: Less configuration: a "pricing client" role and an "operator"
    role.
*   **Cons**: Less control over what each key can do.
*   **Reason for Rejection**: Rejected by author. Separate permissions let a
    quoting system be denied policy writes.

### Alternative W: Winner chosen by most-specific match, highest result, or newest rule

*   **Pros**: No priority numbers to maintain.
*   **Cons**:
    *   "Most specific" has no single definition, so ties need a hidden rule.
    *   Choosing the highest result raises prices whenever rules overlap.
    *   Choosing the newest rule depends on insert order.
*   **Reason for Rejection**: Rejected by author, in favor of an explicit
    priority number.

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **Security**
    *   API keys are never stored in plain text. Only SHA-256 hashes are kept
        in configuration, incoming keys are compared in constant time, and keys
        are sent only over HTTPS.
    *   The pricing tables do not allow UPDATE or DELETE for the API login or
        the ticket login (decision 10).
    *   A fallback authorization policy requires an authenticated caller on
        every endpoint. `ProductsController` is marked `[AllowAnonymous]`.
*   **Performance**
    *   One quote costs three queries: load the policy, run the matching query,
        and insert the quote.
    *   The matching query uses the unique index on `VersionNumber` and the
        unique index on (`VersionId`, `Priority`). At the assumed few hundred
        rules per version, it reads a few pages at most (an estimate).
    *   Assumed load: tens of quotes per second.
*   **Availability**
    *   The API depends on SQL Server for every request. While SQL Server is
        unreachable, quote and policy requests fail. `EnableRetryOnFailure`
        retries short connection failures. This is an accepted limit.
*   **Testing**
    *   Domain unit tests cover policy validation and price rounding.
    *   Integration tests against LocalDB cover matching, the procedure's error
        table, the endpoints, and permissions. Integration tests therefore need
        Windows (decision 13).
*   **Rollout**
    *   The order is:
        1.  Apply the migration, which adds the tables, the type, and the
            procedure.
        2.  The DBA runs `db/scripts/grant-permissions.sql` and
            `db/scripts/verify-permissions.sql`.
        3.  Deploy the API.
        4.  The first change ticket runs `db/tickets/0001-initial-rule-set.sql`.
    *   Until step 4 runs, quote requests return 503 `Pricing.NoActiveVersion`.
        No quote is priced or saved, and the error code names the cause.
*   **Rollback**
    *   Rolling back the API binaries is safe, because the old API ignores the
        new tables.
    *   A bad rule version is "rolled back" by publishing a new version.
    *   For the migration's `Down`, see Risks & Mitigations.

--------------------------------------------------------------------------------

## Risks & Mitigations

| Risk | Type | Mitigation | Accepted because… |
| --- | --- | --- | --- |
| **Large typos pass the database.** A multiplier of 15 instead of 1.5 passes every database check (decision 8). | Correctness | The dry-run diff in the ticket shows the ratio of each change. The ticket review catches it. Saved quotes show `Multiplier`, and the runbook query shows per-version prices. | A ceiling or surcharge-only rule would block real rules (Alternative O). |
| **Privileged logins.** db_owner and sysadmin logins can update or delete pricing rows. | Security | Ticket and API logins have no such rights. `verify-permissions.sql` flags role memberships. Quotes keep copies of the rule values they used. | Ledger tables were rejected (Alternative Q). |
| **The migrator login.** It can alter tables or the procedure. | Security | Migrations are code-reviewed and run only during deployments. | Schema changes need a login with these rights. |
| **Unreviewed base-price edits.** A base-price change through `PUT /api/policies` takes effect without a change ticket. | Correctness | Limited to keys with `policies:write`. Each edit records `ModifiedBy` in the row and its history, and `If-Match` prevents silent overwrites. | Decision 11 keeps policy updates in the API. |
| **Running `Down` in production.** The migration's `Down` drops the tables, destroying policies, rules, and quote history. | Operations | The runbook forbids running `Down` in production once data exists. | It is needed in development. |

--------------------------------------------------------------------------------

## Additional Resources

*   [Concept brief](../../concepts_brief.md): the brainstorm and multi-reviewer
    analysis this design builds on.
*   [EF Core temporal tables](https://learn.microsoft.com/ef/core/providers/sql-server/temporal-tables)
*   [SQL Server ownership chains](https://learn.microsoft.com/sql/relational-databases/security/ownership-chains)

--------------------------------------------------------------------------------

## Future Improvements

*   **OpenTelemetry tracing and exported metrics** for ASP.NET Core and EF Core
    requests.
*   **A cache keyed by version number**, if quote load ever makes the matching
    query a bottleneck.
*   **An automatic ratio threshold** in the procedure, if dry-run review proves
    not to be enough.
*   **A policy impact scan** in the dry run, counting existing policies whose
    winning rule would change.
*   **An idempotency key** on quote creation.

--------------------------------------------------------------------------------

## Detailed Implementation Plan

Paths are relative to the repository root, `C:\Goldfish`. Microsoft packages
are pinned at 10.0.15, matching the existing EF Core references. Test-only
packages are pinned to their latest stable versions at implementation time.

### Phases

Each phase ends with `dotnet build Goldfish.slnx` (0 warnings) and the test
command listed.

| Phase | Contents | Files | Exit check |
| --- | --- | --- | --- |
| 1 | Error types and the shared problem mapping | 1, 2, 45, 46 | Build. The existing Products endpoints return the same status codes. |
| 2 | Domain model | 3–13, 59–62 | `dotnet test tests/Goldfish.Domain.Tests` |
| 3 | Persistence, migration, and procedure | 27–36, 63, 64, 66, 68 | `dotnet test tests/Goldfish.IntegrationTests --filter PublishRuleSetVersionTests` |
| 4 | Readers and handlers | 14–26, 37–41, 65 (connection override only), 69 | `dotnet test tests/Goldfish.IntegrationTests --filter PricingRuleReaderTests` |
| 5 | Authentication and endpoints | 42–44, 47–53, 65 (test keys), 70–72 | `dotnet test` |
| 6 | Operations | 54–58, 67, 73 | `dotnet test`, and the manual check |

The migration is generated once, in phase 3, after files 28–34 exist.

### Target Files to Create/Modify

#### Domain

##### 1. `src/Goldfish.Domain/Abstractions/Result.cs` (Modify)

*   **Type / Scope**: Domain abstraction.
*   **Proposed Changes**:
    *   Add `public enum ErrorType { Failure, Validation, NotFound, PreconditionFailed, Unavailable }`.
    *   Change `Error` to
        `record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)`.
    *   Add static factories `Error.Validation(code, description)`,
        `Error.NotFound(...)`, `Error.PreconditionFailed(...)`,
        `Error.Unavailable(...)`, and `Error.Failure(...)`.
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

*   **Type / Scope**: Domain aggregate, `sealed`, derived from the existing
    `Entity` base class (which holds the `Guid Id`).
*   **Proposed Changes**:
    *   Properties with private setters: `Type`, `CoverageLimit`, `BasePrice`,
        `CreatedOnUtc`, `ModifiedBy`, and `byte[] RowVersion`.
    *   `static Result<Policy> Create(PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy, DateTime utcNow)`
        uses `Guid.CreateVersion7()`.
    *   `Result Update(PolicyType type, decimal coverageLimit, decimal basePrice, string modifiedBy)`.
    *   Both methods reject, with these errors:
        *   a coverage limit of 0 or below, or above
            9,999,999,999,999,999.99 (`InvalidCoverageLimit`);
        *   a base price of 0 or below, or above that bound
            (`InvalidBasePrice`);
        *   an undefined enum value (`InvalidType`);
        *   a value where `decimal.Round(value, 2) != value` (the matching
            `Invalid*` error).
    *   A private parameterless constructor for EF Core.
*   **Why**: Policies hold the inputs to pricing (decision 11, G9). The pattern
    follows the existing `Product.Create` factory, which validates and returns
    `Result<T>`.

##### 5. `src/Goldfish.Domain/Policies/PolicyErrors.cs` (Create)

*   **Type / Scope**: Domain errors.
*   **Proposed Changes**:
    *   `NotFound` (`Policy.NotFound`, NotFound).
    *   `InvalidType`, `InvalidCoverageLimit`, `InvalidBasePrice` (Validation).
    *   `Concurrency` (`Policy.Concurrency`, PreconditionFailed).
*   **Why**: Stable error codes for the policy endpoints.

##### 6. `src/Goldfish.Domain/Policies/IPolicyRepository.cs` (Create)

*   **Type / Scope**: Repository interface.
*   **Proposed Changes**:
    *   `Task<Policy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)`,
        which reads without tracking.
    *   `Task<Policy?> GetForUpdateAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken = default)`,
        which returns a tracked entity whose original `RowVersion` is set to
        `expectedRowVersion`.
    *   `void Add(Policy policy)`.
*   **Why**: A per-aggregate repository, as with Products. The repository
    hands the expected version to EF so the Application stays free of EF types.

##### 7. `src/Goldfish.Domain/Pricing/PricingRuleMatch.cs` (Create)

*   **Type / Scope**: Domain value (`sealed record`).
*   **Proposed Changes**:
    `PricingRuleMatch(Guid VersionId, int VersionNumber, Guid RuleId, string RuleName, int Priority, decimal Multiplier, bool WasCatchAll)`.
*   **Why**: The winning rule as returned by the matching query (decision 6).

##### 8. `src/Goldfish.Domain/Pricing/IPricingRuleReader.cs` (Create)

*   **Type / Scope**: Domain interface.
*   **Proposed Changes**:
    `Task<Result<PricingRuleMatch>> FindMatchAsync(PolicyType type, decimal coverageLimit, CancellationToken cancellationToken = default)`.
    *   Returns `PricingErrors.NoActiveVersion` when no version has been
        published.
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
        *   computes `FinalPrice = Math.Round(UnroundedPrice, 2, MidpointRounding.ToEven)`;
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
    *   `Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)`,
        read without tracking.
    *   `void Add(Quote quote)`.
*   **Why**: Per-aggregate repository.

##### 13. `src/Goldfish.Domain/Abstractions/ConcurrencyException.cs` (Create)

*   **Type / Scope**: Domain exception, `sealed`.
*   **Proposed Changes**: `ConcurrencyException(string message, Exception inner)`.
*   **Why**: The DbContext translates EF's `DbUpdateConcurrencyException` into
    this type (file 28), so `UpdatePolicyHandler` can return
    `Policy.Concurrency` without referencing EF.

#### Application

##### 14. `src/Goldfish.Application/Goldfish.Application.csproj` (Modify)

*   **Type / Scope**: Build configuration.
*   **Proposed Changes**: Add `Microsoft.Extensions.Logging.Abstractions`
    10.0.15.
*   **Why**: Structured logging in the quote handler (G7).

##### 15. `src/Goldfish.Application/Policies/PolicyResponse.cs` (Create)

*   **Type / Scope**: Response DTO.
*   **Proposed Changes**:
    `PolicyResponse(Guid Id, string PolicyType, decimal CoverageLimit, decimal BasePrice, DateTime CreatedOnUtc, string ModifiedBy, string ETag)`,
    with an internal `FromEntity` that sets `ETag` to the quoted base64
    `RowVersion`.
*   **Why**: Response shape for the policy endpoints, and the source of the
    `ETag` header.

##### 16. `src/Goldfish.Application/Policies/PolicyTypeParser.cs` (Create)

*   **Type / Scope**: Internal static helper.
*   **Proposed Changes**: `Result<PolicyType> Parse(string value)`.
    *   Accepts only the exact, case-sensitive names `Commercial` and
        `Personal`.
    *   Rejects numbers and anything else with `PolicyErrors.InvalidType`.
*   **Why**: `Enum.TryParse` accepts numeric strings and can ignore case.
    Either would break the closed list.

##### 17. `src/Goldfish.Application/Policies/CreatePolicy/CreatePolicyHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `CreatePolicyCommand(string PolicyType, decimal CoverageLimit, decimal BasePrice, string ModifiedBy)`.
    *   The handler parses the type, calls `Policy.Create` with
        `TimeProvider`, adds the policy, saves, and returns
        `Result<PolicyResponse>`.
*   **Why**: `POST /api/policies`.

##### 18. `src/Goldfish.Application/Policies/UpdatePolicy/UpdatePolicyHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `UpdatePolicyCommand(Guid Id, string PolicyType, decimal CoverageLimit, decimal BasePrice, byte[] ExpectedRowVersion, string ModifiedBy)`.
    *   The handler:
        1.  calls `GetForUpdateAsync` (`NotFound` if missing);
        2.  parses the type;
        3.  calls `Update`;
        4.  saves, catching `ConcurrencyException` and returning
            `Policy.Concurrency`;
        5.  returns `Result<PolicyResponse>` with the new ETag.
*   **Why**: `PUT /api/policies/{id}` (G9). SQL Server's temporal table records
    the old row.

##### 19. `src/Goldfish.Application/Policies/GetPolicyById/GetPolicyByIdHandler.cs` (Create)

*   **Type / Scope**: Query and handler.
*   **Proposed Changes**: `GetPolicyByIdQuery(Guid Id)` returns
    `Result<PolicyResponse>`.
*   **Why**: `GET /api/policies/{id}`, and the target of `CreatedAtAction`.

##### 20. `src/Goldfish.Application/Quotes/QuoteResponse.cs` (Create)

*   **Type / Scope**: Response DTO.
*   **Proposed Changes**: Fields as listed in the Technical Plan, plus an
    internal `FromEntity`.
*   **Why**: The response explains the price (G4).

##### 21. `src/Goldfish.Application/Quotes/CreateQuote/CreateQuoteHandler.cs` (Create)

*   **Type / Scope**: Command and handler.
*   **Proposed Changes**:
    *   `CreateQuoteCommand(Guid PolicyId, string RequestedBy)`.
    *   **Success path:**
        1.  Load the policy.
        2.  Call `IPricingRuleReader.FindMatchAsync`.
        3.  Call `Quote.Create` with `TimeProvider`.
        4.  Add the quote, then call `SaveChangesAsync` once, with no explicit
            transaction.
        5.  Log at Information:
            `"Quote {QuoteId} for policy {PolicyId} priced by version {VersionNumber} rule {RuleId} (catch-all {WasCatchAll}) multiplier {Multiplier} final {FinalPrice} for {RequestedBy}"`.
        6.  Return `Result<QuoteResponse>`.
    *   **Failure path**, logged before returning the error. Each entry includes
        PolicyId and RequestedBy:

        | Error | Log level |
        | --- | --- |
        | `Policy.NotFound` | Information |
        | `Pricing.NoActiveVersion` | Warning |
        | `Pricing.InvalidActiveVersion` | Error |
*   **Why**: The quote use case. The existing DbContext enables
    `EnableRetryOnFailure`, which rejects user-started transactions unless they
    are wrapped in an execution strategy, so a single save is used.

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
    `Task<Result<ActiveVersionStatus>> GetAsync(CancellationToken cancellationToken = default)`.
    Returns `PricingErrors.NoActiveVersion` when no version has been published.
*   **Why**: An operational read model, kept out of the Domain.

##### 25. `src/Goldfish.Application/Pricing/GetActiveVersion/GetActiveVersionHandler.cs` (Create)

*   **Type / Scope**: Query handler.
*   **Proposed Changes**: Passes the call through to `IPricingStatusReader`.
*   **Why**: Keeps the controller-to-handler pattern consistent.

##### 26. `src/Goldfish.Application/DependencyInjection.cs` (Modify)

*   **Type / Scope**: DI registration.
*   **Proposed Changes**: `AddScoped` for each new handler (files 17–19,
    21, 22, 25).
*   **Why**: This solution registers handlers explicitly and has no assembly
    scanning.

#### Infrastructure

##### 27. `src/Goldfish.Infrastructure/Goldfish.Infrastructure.csproj` (Modify)

*   **Type / Scope**: Build configuration.
*   **Proposed Changes**: Add
    `<InternalsVisibleTo Include="Goldfish.IntegrationTests" />`.
*   **Why**: The integration tests reach the `internal` record types.

##### 28. `src/Goldfish.Infrastructure/Persistence/ApplicationDbContext.cs` (Modify)

*   **Type / Scope**: EF Core context.
*   **Proposed Changes**:
    *   Add `DbSet<Policy> Policies`, `DbSet<Quote> Quotes`, and internal sets
        for the two pricing records (files 31–32).
    *   Override `SaveChangesAsync` to catch `DbUpdateConcurrencyException` and
        rethrow it as `ConcurrencyException`.
*   **Why**: Lets migrations create the tables and the repositories query them,
    and keeps EF exception types out of the Application.

##### 29. `src/Goldfish.Infrastructure/Persistence/Configurations/PolicyConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   `ToTable("Policies", tb => { tb.IsTemporal(t => { t.HasPeriodStart("PeriodStart"); t.HasPeriodEnd("PeriodEnd"); t.UseHistoryTable("PoliciesHistory"); }); tb.HasCheckConstraint("CK_Policies_PolicyType", "..."); tb.HasCheckConstraint("CK_Policies_CoverageLimit_Positive", "..."); tb.HasCheckConstraint("CK_Policies_BasePrice_Positive", "..."); })`.
    *   `Type` maps to column `PolicyType`, `nvarchar(32)`,
        `HasConversion<string>()`, `.UseCollation("Latin1_General_BIN2")`.
    *   Money precision `(18,2)`.
    *   `ModifiedBy` `nvarchar(64)`, required.
    *   `Property(p => p.RowVersion).IsRowVersion()`.
    *   `CreatedOnUtc` as `datetime2`, with a copy of the inline UTC conversion
        lambda from `ProductConfiguration`
        (`v => DateTime.SpecifyKind(v, DateTimeKind.Utc)` on read).
    *   `ValueGeneratedNever` on `Id`.
*   **Why**: Decision 11, G9, and the data model.

##### 30. `src/Goldfish.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.Quotes`, with columns and precisions as in the data
        model.
    *   `ValueGeneratedNever` on `Id`.
    *   `PolicyType` uses `HasConversion<string>()`, `Latin1_General_BIN2`,
        and `CK_Quotes_PolicyType`.
    *   Foreign keys:
        *   to `Policies`;
        *   to `PricingRuleSetVersions`;
        *   a composite (`VersionId`, `RuleId`) to `PricingRules`
            (`VersionId`, `Id`).

        All use `DeleteBehavior.Restrict`.
    *   An index on `PolicyId`.
    *   The UTC conversion on `CreatedOnUtc`.
*   **Why**: The quote snapshot table (G4).

##### 31. `src/Goldfish.Infrastructure/Persistence/Pricing/PricingRuleSetVersionRecord.cs` (Create)

*   **Type / Scope**: Internal persistence class. It is not a domain entity,
    because the API never creates versions.
*   **Proposed Changes**:
    *   Properties: `Guid Id`, `int VersionNumber`, `string ChangeTicketRef`,
        `string ApprovedBy`, `DateTime PublishedOnUtc`.
    *   Navigation `List<PricingRuleRecord> Rules`.
*   **Why**: EF Core needs a mapped type to create and migrate the table.

##### 32. `src/Goldfish.Infrastructure/Persistence/Pricing/PricingRuleRecord.cs` (Create)

*   **Type / Scope**: Internal persistence class.
*   **Proposed Changes**: Properties:
    *   `Guid Id`, `Guid VersionId`
    *   `int Priority`, `string Name`
    *   `string? PolicyType`
    *   `decimal? MinCoverageLimitExclusive`, `decimal? MaxCoverageLimitInclusive`
    *   `decimal Multiplier`
*   **Why**: Same reason as file 31.

##### 33. `src/Goldfish.Infrastructure/Persistence/Configurations/PricingRuleSetVersionConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.PricingRuleSetVersions`.
    *   `Id` with `HasDefaultValueSql("NEWSEQUENTIALID()")`.
    *   Unique indexes `UX_PricingRuleSetVersions_VersionNumber` and
        `UX_PricingRuleSetVersions_ChangeTicketRef`.
    *   `PublishedOnUtc` with default `SYSUTCDATETIME()` and the UTC
        conversion.
*   **Why**: The data model and active-version lookup.

##### 34. `src/Goldfish.Infrastructure/Persistence/Configurations/PricingRuleConfiguration.cs` (Create)

*   **Type / Scope**: EF configuration.
*   **Proposed Changes**:
    *   Table `pricing.PricingRules`.
    *   `Id` with `HasDefaultValueSql("NEWSEQUENTIALID()")`.
    *   Foreign key `FK_PricingRules_Versions`.
    *   `PolicyType` uses `.UseCollation("Latin1_General_BIN2")`.
    *   Unique indexes:
        *   `UX_PricingRules_Version_Priority`;
        *   `UX_PricingRules_Version_Name`;
        *   `UX_PricingRules_Version_Id`;
        *   the filtered `UX_PricingRules_Version_CatchAll`
            (`[PolicyType] IS NULL AND [MinCoverageLimitExclusive] IS NULL AND [MaxCoverageLimitInclusive] IS NULL`).
    *   Named CHECK constraints:
        *   `CK_PricingRules_Priority_Min` (`Priority >= 1`)
        *   `CK_PricingRules_Multiplier_Positive` (`Multiplier > 0`)
        *   `CK_PricingRules_PolicyType` (`PolicyType IS NULL OR PolicyType IN ('Commercial','Personal')`)
        *   `CK_PricingRules_CoverageRange` (`MinCoverageLimitExclusive IS NULL OR MaxCoverageLimitInclusive IS NULL OR MinCoverageLimitExclusive < MaxCoverageLimitInclusive`)
    *   Precisions as in the data model.
*   **Why**: G3 and G6, plus decisions 2, 3, 5, and 8.

##### 35. `src/Goldfish.Infrastructure/Persistence/Migrations/<timestamp>_AddPolicyPricing.cs` (Create, generated then edited)

*   **Type / Scope**: EF Core migration.
*   **Proposed Changes**:
    *   Generated with
        `dotnet ef migrations add AddPolicyPricing -p src/Goldfish.Infrastructure -s src/Goldfish.Api -o Persistence/Migrations`.
        This creates the `pricing` schema, the tables, the indexes, the
        constraints, and the temporal table.
    *   Then append `migrationBuilder.Sql` calls for:
        1.  `CREATE TYPE pricing.PricingRuleInput AS TABLE (...)`, with the
            columns from the procedure contract.
        2.  `CREATE PROCEDURE pricing.PublishRuleSetVersion`, implementing the
            contract, steps, and error table from the Technical Plan
            (`SET XACT_ABORT ON`, `THROW 50001`–`50005` with the messages in
            the error table).
    *   `Down` drops the procedure, then the type, then the tables.
*   **Why**: Schema and publishing (decision 12). Rule data is not included
    (non-goal).

##### 36. `src/Goldfish.Infrastructure/Persistence/Migrations/<timestamp>_AddPolicyPricing.Designer.cs` and `ApplicationDbContextModelSnapshot.cs` (Create / Modify, generated)

*   **Type / Scope**: EF Core generated files.
*   **Proposed Changes**: Generated by `dotnet ef`. Do not edit by hand.
*   **Why**: EF migration bookkeeping.

##### 37. `src/Goldfish.Infrastructure/Repositories/PolicyRepository.cs` (Create)

*   **Type / Scope**: `internal sealed` repository.
*   **Proposed Changes**:
    *   `GetByIdAsync` reads with `AsNoTracking`.
    *   `GetForUpdateAsync` reads with tracking and sets
        `Entry(policy).Property(p => p.RowVersion).OriginalValue = expectedRowVersion`.
    *   `Add` adds the policy.
*   **Why**: Implements `IPolicyRepository`, including the `If-Match` check.

##### 38. `src/Goldfish.Infrastructure/Repositories/QuoteRepository.cs` (Create)

*   **Type / Scope**: `internal sealed` repository.
*   **Proposed Changes**: `GetByIdAsync` reads with `AsNoTracking`. `Add` adds
    the quote.
*   **Why**: Implements `IQuoteRepository`.

##### 39. `src/Goldfish.Infrastructure/Pricing/SqlPricingRuleReader.cs` (Create)

*   **Type / Scope**: `internal sealed` implementation of `IPricingRuleReader`.
*   **Proposed Changes**:
    *   Runs the Technical Plan's matching query through
        `Database.SqlQuery<MatchRow>($"...")`. The policy type (as the enum's
        string name) and the coverage limit are interpolation holes, so EF
        sends them as parameters.
    *   Declares
        `private sealed record MatchRow(Guid VersionId, int VersionNumber, Guid? RuleId, string? Name, int? Priority, decimal? Multiplier, bool? WasCatchAll)`.
    *   Calls `ToListAsync` and does not compose further, because EF cannot
        wrap a query that starts with `WITH`.
    *   Maps the results:
        *   an empty list maps to `NoActiveVersion`;
        *   a row with a null `RuleId` maps to `InvalidActiveVersion`;
        *   otherwise the row maps to `PricingRuleMatch`, with `Name` becoming
            `RuleName`.
*   **Why**: Decision 6, with no cache (G2).

##### 40. `src/Goldfish.Infrastructure/Pricing/SqlPricingStatusReader.cs` (Create)

*   **Type / Scope**: `internal sealed` implementation of
    `IPricingStatusReader`.
*   **Proposed Changes**: Reads the internal version set with `AsNoTracking`,
    `OrderByDescending(v => v.VersionNumber)`, and `FirstOrDefaultAsync`.
    Returns `NoActiveVersion` when no version exists.
*   **Why**: The active-version endpoint (G7).

##### 41. `src/Goldfish.Infrastructure/DependencyInjection.cs` (Modify)

*   **Type / Scope**: DI registration.
*   **Proposed Changes**: Register `IPolicyRepository`, `IQuoteRepository`,
    `IPricingRuleReader`, and `IPricingStatusReader`, all scoped.
*   **Why**: Connects the interfaces to their implementations.

#### Api

##### 42. `src/Goldfish.Api/Authentication/ApiKeyOptions.cs` (Create)

*   **Type / Scope**: Options classes.
*   **Proposed Changes**:
    *   `ApiKeyOptions { public List<ApiKeyEntry> Keys { get; set; } = new(); }`.
    *   `ApiKeyEntry`, with `{ get; set; }` properties `string Name`,
        `string KeyHash`, and `string[] Permissions`.
    *   Bound from the `ApiKeys` configuration section.
    *   Validated on start:
        *   names are unique and 1–64 characters;
        *   each hash is 64 hex characters;
        *   each permission is a known value.
*   **Why**: Decision 9. Only hashes are stored.

##### 43. `src/Goldfish.Api/Authentication/Permissions.cs` (Create)

*   **Type / Scope**: Constants and an extension method.
*   **Proposed Changes**:
    *   Constants: `policies:read`, `policies:write`, `quotes:create`,
        `quotes:read`, `pricing:read`.
    *   `AddPermissionPolicies(this AuthorizationBuilder)` registers one
        authorization policy per permission, each requiring the claim
        `permission` with that value.
*   **Why**: The per-key permissions in the API table.

##### 44. `src/Goldfish.Api/Authentication/ApiKeyAuthenticationHandler.cs` (Create)

*   **Type / Scope**: `AuthenticationHandler<AuthenticationSchemeOptions>`,
    scheme `ApiKey`.
*   **Proposed Changes**:
    *   The constructor takes the standard handler arguments plus
        `IOptionsMonitor<ApiKeyOptions>`.
    *   Reads the `X-Api-Key` header. If it is missing, returns `NoResult`,
        which leads to a 401 challenge.
    *   Hashes the value with SHA-256, and compares it against
        `Convert.FromHexString(entry.KeyHash)` for each entry using
        `CryptographicOperations.FixedTimeEquals`.
    *   On a match, issues a principal from
        `new ClaimsIdentity(claims, "ApiKey")`, with a `ClaimTypes.Name` claim
        set to the entry name and one `permission` claim per permission.
    *   Otherwise returns `Fail`. The key value is never logged.
*   **Why**: Decision 9.

##### 45. `src/Goldfish.Api/Extensions/ResultExtensions.cs` (Create)

*   **Type / Scope**: Controller extension.
*   **Proposed Changes**: `IActionResult ToProblem(this ControllerBase c, Error error)`
    maps:
    *   Validation to 400;
    *   NotFound to 404;
    *   PreconditionFailed to 412;
    *   Unavailable to 503;
    *   Failure to 500.

    It uses `Problem(statusCode, title: error.Code, detail: error.Description)`.
*   **Why**: The single error mapping (Technical Plan, Error handling).

##### 46. `src/Goldfish.Api/Controllers/ProductsController.cs` (Modify)

*   **Type / Scope**: Controller.
*   **Proposed Changes**:
    *   Replace the two inline `Problem(...)` calls with
        `this.ToProblem(result.Error)`. Status codes are unchanged.
    *   Add `[AllowAnonymous]` to the class.
*   **Why**: Uses the shared mapping, and keeps the sample open under the
    fallback authorization policy.

##### 47. `src/Goldfish.Api/Controllers/PoliciesController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/policies")]`.
*   **Proposed Changes**:
    *   `POST` (`policies:write`) returns 201 with `CreatedAtAction(GetById)`
        and the `ETag` header.
    *   `GET {id:guid}` (`policies:read`) sets the `ETag` header.
    *   `PUT {id:guid}` (`policies:write`):
        *   reads `If-Match`, and returns 428 when it is missing;
        *   decodes the quoted base64 value into `ExpectedRowVersion`;
        *   returns 200 with the new `ETag`.
    *   `POST {policyId:guid}/quotes` (`quotes:create`) returns 201 with
        `CreatedAtAction(nameof(QuotesController.GetById), "Quotes", ...)`.
    *   The caller name, passed as `ModifiedBy` or `RequestedBy`, is
        `User.Identity?.Name ?? throw new InvalidOperationException("Authenticated caller has no name.")`.
    *   Request records `CreatePolicyRequest(string PolicyType, decimal CoverageLimit, decimal BasePrice)`
        and `UpdatePolicyRequest` (same fields) are declared in the file, as
        `CreateProductRequest` is today.
    *   Every action has `[Authorize(Policy = ...)]` and
        `ProducesResponseType` attributes.
*   **Why**: The policy and quote routes from the API table.

##### 48. `src/Goldfish.Api/Controllers/QuotesController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/quotes")]`.
*   **Proposed Changes**: `GET {id:guid}` (`quotes:read`).
*   **Why**: Reading a saved quote, and the target of the quote `POST`.

##### 49. `src/Goldfish.Api/Controllers/PricingController.cs` (Create)

*   **Type / Scope**: Controller, `[Route("api/pricing")]`.
*   **Proposed Changes**: `GET active-version` (`pricing:read`) returns
    `ActiveVersionStatus`.
*   **Why**: The operator view (G7).

##### 50. `src/Goldfish.Api/Program.cs` (Modify)

*   **Type / Scope**: Composition root.
*   **Proposed Changes**:
    *   `services.AddOptions<ApiKeyOptions>().Bind(...).Validate(...).ValidateOnStart()`.
    *   `AddAuthentication("ApiKey").AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null)`.
    *   `AddAuthorizationBuilder().AddPermissionPolicies().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`.
    *   `app.UseAuthentication()` before `UseAuthorization()`.
    *   No `public partial class Program;` line, because .NET 10 generates it
        (analyzer ASP0027).
*   **Why**: Enables authentication (G8).

##### 51. `src/Goldfish.Api/appsettings.json` (Modify)

*   **Type / Scope**: Configuration.
*   **Proposed Changes**: Add `"ApiKeys": { "Keys": [] }`, with no keys
    committed.
*   **Why**: With no keys configured, every protected endpoint returns 401.

##### 52. `src/Goldfish.Api/Goldfish.Api.csproj` (Modify)

*   **Type / Scope**: Build configuration.
*   **Proposed Changes**: Add a `UserSecretsId` so development key hashes can
    live in user secrets.
*   **Why**: Keeps development keys out of source control.

##### 53. `src/Goldfish.Api/Goldfish.Api.http` (Modify)

*   **Type / Scope**: Manual request samples.
*   **Proposed Changes**:
    *   Set the host to `https://localhost:7258` (the https profile in
        `launchSettings.json`).
    *   Add sample requests for creating, getting, and updating a policy (with
        `If-Match`), creating and getting a quote, and the active-version
        endpoint.
    *   Each request sends an `X-Api-Key: {{apiKey}}` variable.
*   **Why**: Manual verification of the CUJs. Keys are sent only over HTTPS.

#### Database scripts (outside migrations)

##### 54. `db/scripts/grant-permissions.sql` (Create)

*   **Type / Scope**: DBA-run T-SQL, using sqlcmd variables `$(ApiUser)` and
    `$(PublisherUser)`, which are database user names.
*   **Proposed Changes**:
    *   Creates each user `FOR LOGIN` only when a login with that name exists
        and the user does not, checked in `sys.database_principals`.
    *   Grants the rights in the Technical Plan's permissions table, including
        `dbo.Products` and `TYPE::pricing.PricingRuleInput`.
    *   Explicitly `DENY UPDATE, DELETE` on the three pricing tables to both
        users.
    *   Contains no `GO` separators, so it runs as a single batch.
*   **Why**: Decision 10. Logins differ per environment, so this is not part
    of the migrations.

##### 55. `db/scripts/verify-permissions.sql` (Create)

*   **Type / Scope**: Read-only T-SQL.
*   **Proposed Changes**:
    *   Lists the actual rights of both users on the pricing, policy, product,
        and type objects from `sys.database_permissions`.
    *   Returns a `Problem` column set to `UPDATE/DELETE granted` or
        `privileged role` for any UPDATE or DELETE right on pricing tables, and
        for any membership in db_owner, db_datawriter, or db_ddladmin.
*   **Why**: Lets a reviewer confirm the no-edit guarantee in each
    environment.

##### 56. `db/templates/publish-rule-set-version.sql` (Create)

*   **Type / Scope**: Ticket script template.
*   **Proposed Changes**:
    *   `DECLARE @Rules pricing.PricingRuleInput;`.
    *   One `INSERT INTO @Rules` row per rule, with comments explaining each
        column.
    *   `DECLARE @NewVersionNumber int;`.
    *   A first `EXEC pricing.PublishRuleSetVersion` with `@DryRun = 1`, whose
        output the reviewer attaches to the ticket. Then the real `EXEC` with
        `@DryRun = 0`.
    *   Literal placeholders `@ChangeTicketRef = 'CHG-0000'`,
        `@ApprovedBy = 'name@example'`, and `@BaseVersionNumber = 0`.
    *   Comments say:
        *   check `@BaseVersionNumber` against the active-version endpoint;
        *   the change is live the moment the script commits.
*   **Why**: Every ticket script has the same reviewable shape.

##### 57. `db/tickets/0001-initial-rule-set.sql` (Create)

*   **Type / Scope**: The first change-ticket script.
*   **Proposed Changes**: Publishes version 1 with `@BaseVersionNumber = 0` and
    two rules:
    *   "Large commercial" (priority 10, Commercial, more than 1,000,000,
        multiplier 1.5);
    *   "Default" (priority 1000, no conditions, multiplier 1.0).
*   **Why**: Rule data arrives through the ticket path, not migrations. This
    script shows the template in use.

#### Documentation

##### 58. `docs/pricing-rules.md` (Create)

*   **Type / Scope**: Runbook.
*   **Proposed Changes**:
    *   How rules match: priority numbers, typed conditions and their
        boundaries, the catch-all, and rounding.
    *   How to write and review a ticket script: setting the base version,
        reading the dry-run diff, and the procedure's error table.
    *   That a version is live the moment its script commits.
    *   How to correct a mistaken version by publishing a new one.
    *   The monitoring query over `pricing.Quotes`, grouped by VersionNumber and
        WasCatchAll.
    *   How to generate an API key
        (`[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`,
        converted to base64url) and its SHA-256 hash, and how to rotate it.
    *   The rollout order, and the rule never to run the migration's `Down` in
        production.
    *   The risks from Risks & Mitigations.
*   **Why**: Analysts, reviewers, and DBAs work outside the code and need one
    reference.

#### Tests

##### 59. `Goldfish.slnx` (Modify)

*   **Type / Scope**: Solution.
*   **Proposed Changes**: Add the two test projects under a `/tests/` folder.
*   **Why**: `dotnet test` runs everything.

##### 60. `tests/Goldfish.Domain.Tests/Goldfish.Domain.Tests.csproj` (Create)

*   **Type / Scope**: xUnit test project, net10.0.
*   **Proposed Changes**:
    *   References `Goldfish.Domain`.
    *   Packages, each pinned exactly: `xunit.v3`,
        `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, and
        `coverlet.collector`.
    *   `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
*   **Why**: Unit tests for domain rules, with no database.

##### 61. `tests/Goldfish.Domain.Tests/Policies/PolicyTests.cs` (Create)

*   **Type / Scope**: Unit tests.
*   **Proposed Changes**:
    *   Create and update succeed with valid values and set `ModifiedBy`.
    *   They reject:
        *   a coverage limit or base price of 0 or below;
        *   a value above the upper bound;
        *   an undefined enum value;
        *   a value with 3 significant decimal places (100.255).
    *   They accept `100.250`.
*   **Why**: Policy validation.

##### 62. `tests/Goldfish.Domain.Tests/Quotes/QuoteTests.cs` (Create)

*   **Type / Scope**: Unit tests.
*   **Proposed Changes**:
    *   1,234.57 × 1.5 gives unrounded 1,851.855 and final 1,851.86.
    *   100.25 × 0.5 = 50.125 rounds to 50.12 (half to even, down).
    *   100.35 × 0.5 = 50.175 rounds to 50.18 (half to even, up).
    *   The snapshot fields are copied from the policy and the match.
*   **Why**: Decision 7 and G4.

##### 63. `tests/Goldfish.IntegrationTests/Goldfish.IntegrationTests.csproj` (Create)

*   **Type / Scope**: xUnit test project, net10.0.
*   **Proposed Changes**:
    *   References `Goldfish.Api` and `Goldfish.Infrastructure`.
    *   Packages, each pinned exactly: `xunit.v3`,
        `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
        `coverlet.collector`, and `Microsoft.AspNetCore.Mvc.Testing` 10.0.15.
        `Microsoft.Data.SqlClient` comes through EF Core SqlServer, so it has
        no separate reference.
    *   `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
*   **Why**: Runs the edge cases against a real SQL Server, because matching
    runs in SQL (decision 13).

##### 64. `tests/Goldfish.IntegrationTests/Infrastructure/LocalDbFixture.cs` (Create)

*   **Type / Scope**: xUnit collection fixture, shared by every integration
    test through one `[Collection("Database")]`, so they run one at a time.
*   **Proposed Changes**:
    *   Builds the connection string
        `Server=(localdb)\MSSQLLocalDB;Database=Goldfish_Test_<guid>;Trusted_Connection=True;TrustServerCertificate=True`
        and exposes it as `ConnectionString`.
    *   Applies the migrations with `Database.MigrateAsync()`.
    *   Drops the database on dispose.
    *   `ResetAsync()` runs before each test, as the developer's Windows login
        (which is sysadmin on LocalDB). It:
        1.  deletes rows in this order: Quotes, PricingRules,
            PricingRuleSetVersions, Policies;
        2.  turns `SYSTEM_VERSIONING` off;
        3.  clears `PoliciesHistory`;
        4.  turns versioning back on.
*   **Why**: Decision 13. Each run uses an isolated database, and each test
    starts empty.

##### 65. `tests/Goldfish.IntegrationTests/Infrastructure/GoldfishApiFactory.cs` (Create)

*   **Type / Scope**: `WebApplicationFactory<Program>`.
*   **Proposed Changes**:
    *   **Phase 4:**
        *   Overrides the connection with
            `builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString)`,
            which is read before `AddInfrastructure` runs.
        *   After the host is built, asserts that the resolved database name
            starts with `Goldfish_Test_`.
        *   Tests resolve services from `factory.Services.CreateScope()`.
    *   **Phase 5:**
        *   Overrides `ApiKeys:Keys` with three keys. Each is a fixed 32-byte
            test value, with its SHA-256 hash computed in the factory, not
            stored:
            *   `full`, with all permissions;
            *   `quoter`, with `quotes:create`;
            *   `reader`, with `policies:read`, `quotes:read`, and
                `pricing:read`.
        *   Provides a `CreateClient(string keyName)` helper that sets the
            `X-Api-Key` header.
*   **Why**: End-to-end tests with real authentication, which can never touch
    the developer database.

##### 66. `tests/Goldfish.IntegrationTests/Infrastructure/RuleSetPublisher.cs` (Create)

*   **Type / Scope**: Test helper.
*   **Proposed Changes**:
    *   Calls `pricing.PublishRuleSetVersion` through `SqlCommand`, passing a
        `DataTable` whose columns match `pricing.PricingRuleInput` in order as
        a structured parameter.
    *   Exposes `BaseVersionNumber`, `DryRun`, and the `@NewVersionNumber`
        output.
    *   Returns the dry-run rows.
*   **Why**: Tests publish rules the same way ticket scripts do.

##### 67. `tests/Goldfish.IntegrationTests/Infrastructure/SqlScriptLoader.cs` (Create)

*   **Type / Scope**: Test helper.
*   **Proposed Changes**: Reads a `.sql` file, replaces `$(Name)` variables
    from a dictionary, and runs the text as one batch through `SqlCommand`.
*   **Why**: Lets the permission test run `grant-permissions.sql` without
    sqlcmd.

##### 68. `tests/Goldfish.IntegrationTests/Pricing/PublishRuleSetVersionTests.cs` (Create)

*   **Type / Scope**: Integration tests.
*   **Proposed Changes**:
    *   One test per row of the procedure's error table, each asserting that
        row's error number (50001–50005, 547, or 2601) and the constraint name
        where there is one.
    *   After each rejection, the version count is unchanged.
    *   A valid publish sets `@NewVersionNumber` to `@BaseVersionNumber + 1`.
    *   Stale base: publish from base 0, then again from base 0. The second
        fails with 50003 (CUJ 7).
    *   Two concurrent publishes from the same base produce exactly one new
        version, and the other fails with 2601.
    *   A dry run returns correct Added, Removed, Changed, and Unchanged rows
        with ratios, and inserts nothing.
*   **Why**: CUJs 4 and 7, G2, G6, and decision 12.

##### 69. `tests/Goldfish.IntegrationTests/Pricing/PricingRuleReaderTests.cs` (Create)

*   **Type / Scope**: Integration tests, resolving `IPricingRuleReader` from a
    scope.
*   **Proposed Changes**:
    *   With no version, the result is `NoActiveVersion`.
    *   A coverage limit of exactly 1,000,000 falls through to the catch-all.
    *   A limit of 1,000,000.01 matches "Large commercial".
    *   The lower priority number wins when two rules match.
    *   A `Personal` policy never matches a `Commercial` rule.
    *   A newly published version is used on the next call.
    *   With versions 1 and 2 published, version 2 is the one matched.
    *   Inserting `commercial` into a `PolicyType` column fails
        `CK_PricingRules_PolicyType` (binary collation).
*   **Why**: Decisions 2–6, G2, and G3.

##### 70. `tests/Goldfish.IntegrationTests/Api/QuoteEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   CUJ 1 returns 201, and the saved row's values match the response.
    *   CUJ 2 returns 201 with `WasCatchAll = true`.
    *   No key returns 401 (CUJ 8). The `reader` key returns 403.
    *   An unknown policy returns 404.
    *   No active version returns 503 (CUJ 9).
    *   A saved quote keeps its version after a new version is published, and
        `GET /api/quotes/{id}` returns 200 for the `reader` key.
*   **Why**: CUJs 1–3, 8, and 9, G4, and G8.

##### 71. `tests/Goldfish.IntegrationTests/Api/PolicyEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   Create returns 201 with an `ETag`.
    *   A lowercase `commercial` or a numeric `"0"` type returns 400.
    *   Update with the current ETag returns 200 with a new ETag, and
        `dbo.PoliciesHistory` holds the old row with its `ModifiedBy`.
    *   Update with a stale ETag returns 412. Update without `If-Match` returns
        428.
    *   Update of an unknown id returns 404.
    *   The `quoter` key returns 403 on `PUT`.
    *   Anonymous `GET /api/products/{id}` still works.
*   **Why**: CUJ 6, decision 11, G8, and G9.

##### 72. `tests/Goldfish.IntegrationTests/Api/ActiveVersionEndpointTests.cs` (Create)

*   **Type / Scope**: HTTP integration tests.
*   **Proposed Changes**:
    *   Shows the latest version's number and ticket reference, and changes
        after a new version is published.
    *   Returns 503 when no version exists.
*   **Why**: CUJ 5 and G7.

##### 73. `tests/Goldfish.IntegrationTests/Database/PermissionTests.cs` (Create)

*   **Type / Scope**: Integration tests.
*   **Proposed Changes**:
    *   Creates the users `api_test` and `publisher_test` `WITHOUT LOGIN`.
    *   Runs `db/scripts/grant-permissions.sql` through `SqlScriptLoader`, with
        `ApiUser = api_test` and `PublisherUser = publisher_test`. Runs it
        twice, and asserts that the second run succeeds.
    *   Using `EXECUTE AS USER`, checks that:
        *   `UPDATE` and `DELETE` on all three pricing tables fail for both
            users;
        *   a direct `INSERT` into `pricing.PricingRules` fails for
            `publisher_test`;
        *   `publisher_test` can declare a `pricing.PricingRuleInput` variable
            and `EXEC pricing.PublishRuleSetVersion`;
        *   `api_test` can insert quotes and read and insert `dbo.Products`.
*   **Why**: G5 and decision 10.

### Verification Plan

*   **Compilation/Build**: `dotnet build Goldfish.slnx` finishes with 0
    warnings and 0 errors.
*   **Migrations**:
    *   Run `dotnet tool restore` first. `dotnet-tools.json` pins dotnet-ef
        10.0.15.
    *   `dotnet ef migrations has-pending-model-changes -p src/Goldfish.Infrastructure -s src/Goldfish.Api`
        reports no changes.
    *   `dotnet ef database update -p src/Goldfish.Infrastructure -s src/Goldfish.Api --connection "Server=(localdb)\MSSQLLocalDB;Database=Goldfish_Verify;Trusted_Connection=True;TrustServerCertificate=True"`
        applies cleanly to an empty database.
*   **Unit Tests**: `dotnet test tests/Goldfish.Domain.Tests`.
*   **Integration Tests**: `dotnet test tests/Goldfish.IntegrationTests`. They
    need Windows with LocalDB `MSSQLLocalDB`. Any CI pipeline added later must
    use Windows agents.
*   **Compiler warnings**: `TreatWarningsAsErrors` is set in both test
    projects. Nullable reference types stay enabled in all projects.
*   **Test Coverage Requirement**:
    *   Every CUJ, and every row of the procedure's error table, has at least
        one test.
    *   `dotnet test --collect "XPlat Code Coverage"` (coverlet.collector)
        produces the report. `Policy` and `Quote` in `Goldfish.Domain` need 90%
        or more line coverage, checked from that report.
*   **Manual**:
    1.  Run the API with the https profile.
    2.  `POST /api/policies` with the `full` development key: Commercial,
        2,000,000, 1,234.57.
    3.  Run `db/tickets/0001-initial-rule-set.sql` with sqlcmd as the
        developer's Windows login against the development LocalDB.
    4.  Use `Goldfish.Api.http` to request a quote, and confirm the CUJ 1
        numbers.

### Implementation notes

These differences from the plan above were found while building it.

*   **Test runner and coverage.** The .NET 10 SDK runs xunit.v3 4.x only on
    Microsoft.Testing.Platform. Because of this:
    *   `global.json` opts in with `"test": { "runner": "Microsoft.Testing.Platform" }`.
    *   Coverage uses `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0
        instead of `coverlet.collector`, which works only with VSTest.
    *   The commands are `dotnet test --project <test project>` and
        `dotnet test --solution Goldfish.slnx --coverage --coverage-output-format cobertura`.
    *   Measured coverage: `Policy` 93.9% and `Quote` 95.5%. The uncovered
        lines are the private constructors that EF Core uses.
*   **Pinned test packages:**
    *   `xunit.v3` 4.0.3
    *   `xunit.runner.visualstudio` 4.0.1
    *   `Microsoft.NET.Test.Sdk` 18.12.0
*   **Reader tests** resolve `IPricingRuleReader` from the real
    `AddApplication()` and `AddInfrastructure()` registrations, built by
    `LocalDbFixture.CreateServices()`, rather than from the web host. The
    endpoint tests use `GoldfishApiFactory`.
*   **Extra helper files:**
    *   `src/Goldfish.Infrastructure/Persistence/PersistenceConventions.cs`
        holds the shared schema, collation, length, and policy-type list.
    *   `tests/Goldfish.IntegrationTests/Api/ApiTestExtensions.cs` creates
        policies over HTTP in tests.
*   **Test database reset** deletes from `dbo.PoliciesHistory` through dynamic
    SQL. SQL Server checks the whole batch before running it, and rejects that
    delete while system versioning is still on.
*   **`verify-permissions.sql`** applies `COLLATE DATABASE_DEFAULT` to every
    text column. The catalog views use different collations, which made the
    `UNION` fail. `PermissionTests` now runs the verify script and asserts that
    it reports no problems.
*   **Malformed `If-Match`** values that are not a valid ETag return 412
    `Policy.Concurrency`, the same as a stale ETag.

--------------------------------------------------------------------------------

<!-- Provenance: Indicates initial structural drafting via EGM. -->

*Drafted with EGM*
