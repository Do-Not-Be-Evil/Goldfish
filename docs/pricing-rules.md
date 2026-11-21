# Pricing Rules Runbook

This is the operating guide for policy pricing in the Goldfish API: for analysts writing rule changes, for
ticket reviewers, and for the DBAs who run scripts. The design is in
[designs/policy-pricing-rules.md](designs/policy-pricing-rules.md).

## How a price is calculated

1.  The API loads the policy, which has a type, a coverage limit, and a base price.
2.  It reads the **active version**, which is always the published version with the highest version
    number. Nothing is cached, so a new version is used by the next quote after its script commits.
3.  Within that version, the matching rule with the **lowest priority number** wins. A rule matches when
    every one of its conditions is true:
    *   `PolicyType` is NULL (meaning any type) or equals the policy's type, in exact case;
    *   `MinCoverageLimitExclusive` is NULL, or the coverage limit is **above** it, so a policy at exactly
        1,000,000 does not match "above 1,000,000";
    *   `MaxCoverageLimitInclusive` is NULL, or the coverage limit is **at most** it.
4.  Final price = base price × multiplier, rounded to 2 decimal places half-to-even ("banker's
    rounding"). For example, 50.125 becomes 50.12 and 50.175 becomes 50.18. The unrounded value is stored
    too.
5.  Every version has exactly one **catch-all** rule. It has no conditions and the largest priority
    number, so every policy gets a price. A quote priced by it has `WasCatchAll = true`.

Each quote stores the version, the rule, the policy values it used, the multiplier, and both prices. A
past quote can therefore be explained even after rules or the policy change.

## Publishing a rule change

A version is a **full copy** of the rule set. To change one rule, publish every rule again with that one
changed.

1.  Copy [`db/templates/publish-rule-set-version.sql`](../db/templates/publish-rule-set-version.sql)
    into the change ticket.
2.  Set `@ChangeTicketRef` and `@ApprovedBy`.
3.  Set `@BaseVersionNumber` to the version you built the script from. Find it with
    `GET /api/pricing/active-version`, or with
    `SELECT MAX(VersionNumber) FROM pricing.PricingRuleSetVersions`.
4.  Run the script with `@DryRun = 1`. The output has one row per rule with Name, OldMultiplier,
    NewMultiplier, Ratio, and Change (`Added`, `Removed`, `Changed`, or `Unchanged`). Nothing is saved.
    Attach this output to the ticket.
5.  **Reviewer:** check every `Changed` row's Ratio. A ratio of 10.0000 means a multiplier went from,
    for example, 1.5 to 15. The database accepts any multiplier above 0, so this review is the only check
    against a typo.
6.  After approval, run the script with `@DryRun = 0`. **The change is live the moment the script
    commits.** No pricing change is tied to a date or time, so run it when it should apply.

### Correcting a mistake

Published versions are never edited or deleted. To correct one, publish a new version with the right
values, using `@BaseVersionNumber` set to the mistaken version. Quotes already priced by the mistaken
version keep their prices and show which version priced them.

### Procedure errors

| Error | Meaning | What to do |
| --- | --- | --- |
| 50001 | Blank ticket reference or approver | Fill in both. |
| 50002 | Empty rule set | List every rule. |
| 50003 | Base version is not the latest | Someone published first. Rebuild the script from the latest version. |
| 50004 | Not exactly one catch-all rule | Exactly one rule must have all three conditions NULL. |
| 50005 | Catch-all does not have the largest priority number | Give the catch-all a larger priority number than every other rule. |
| 2601 `UX_PricingRuleSetVersions_VersionNumber` | Another publish from the same base committed at the same moment | Rebuild from the latest version. |
| 547 `CK_PricingRules_Priority_Min` | A priority below 1 | Use 1 or more. |
| 547 `CK_PricingRules_Multiplier_Positive` | A multiplier of 0 or below | Use a multiplier above 0. |
| 547 `CK_PricingRules_CoverageRange` | A minimum not below the maximum | Fix the range. |
| 547 `CK_PricingRules_PolicyType` | Unknown policy type, or wrong case | Use `Commercial` or `Personal` exactly. |
| 2601 `UX_PricingRules_Version_Priority` | Two rules with the same priority | Make priorities unique. |
| 2601 `UX_PricingRules_Version_Name` | Two rules with the same name | Make names unique. |
| 2601 `UX_PricingRuleSetVersions_ChangeTicketRef` | Ticket reference already used | Each ticket publishes once. |

Any error rolls back the whole script, so nothing is half-published.

## Monitoring

Catch-all rate and volume per version:

```sql
SELECT VersionNumber, WasCatchAll, COUNT(*) AS Quotes,
       MIN(CreatedOnUtc) AS FirstQuoteUtc, MAX(CreatedOnUtc) AS LastQuoteUtc
FROM pricing.Quotes
GROUP BY VersionNumber, WasCatchAll
ORDER BY VersionNumber DESC, WasCatchAll;
```

A rise in catch-all quotes after a publish usually means a rule's conditions were narrowed by mistake.

Each quote is also logged at Information:
`Quote {QuoteId} for policy {PolicyId} priced by version {VersionNumber} rule {RuleId} ...`.
`Pricing.NoActiveVersion` is logged as a Warning, and `Pricing.InvalidActiveVersion` as an Error.

## API keys

*   Generate a key, which is 32 random bytes in base64url form:

    ```powershell
    $key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_')
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key)))
    ```

*   Give `$key` to the calling system. Store only `$hash`, as `ApiKeys:Keys:<n>:KeyHash`, together with a
    `Name` (1–64 characters; it is recorded as `RequestedBy` and `ModifiedBy`) and the key's
    `Permissions`. In development use user secrets:

    ```powershell
    dotnet user-secrets set "ApiKeys:Keys:0:Name" "dev" --project src/Goldfish.Api
    dotnet user-secrets set "ApiKeys:Keys:0:KeyHash" $hash --project src/Goldfish.Api
    dotnet user-secrets set "ApiKeys:Keys:0:Permissions:0" "policies:write" --project src/Goldfish.Api
    ```

*   Permissions:
    *   `policies:read`
    *   `policies:write`
    *   `quotes:create`
    *   `quotes:read`
    *   `pricing:read`
*   Keys are sent only over HTTPS, in the `X-Api-Key` header.
*   **Rotating a key:**
    1.  Add the new entry and restart the API.
    2.  Move callers to the new key.
    3.  Remove the old entry and restart again.

## Rollout

1.  Apply the migration (`dotnet ef database update`, run by the migrator login).
2.  The DBA runs [`db/scripts/grant-permissions.sql`](../db/scripts/grant-permissions.sql) and then
    [`db/scripts/verify-permissions.sql`](../db/scripts/verify-permissions.sql). The verify script must
    show no rows with a `Problem`.
3.  Deploy the API.
4.  Run the first ticket, [`db/tickets/0001-initial-rule-set.sql`](../db/tickets/0001-initial-rule-set.sql).
    Until then, quote requests return 503 `Pricing.NoActiveVersion`.

**Never run the `AddPolicyPricing` migration's `Down` in production once data exists.** It drops the
policy, rule, and quote tables.

## Accepted risks

| Risk | Mitigation |
| --- | --- |
| A multiplier typo such as 15 instead of 1.5 passes the database. | The dry-run Ratio in the ticket, and ticket review. |
| db_owner, sysadmin, and migrator logins can still change pricing rows or the procedure. | The ticket and API logins cannot. `verify-permissions.sql` flags privileged roles. Quotes keep copies of the rule values they used. |
| Base-price edits through `PUT /api/policies` skip ticket review. | Only `policies:write` keys can make them. Each edit records `ModifiedBy`, the history table keeps every earlier row, and `If-Match` prevents silent overwrites. |
