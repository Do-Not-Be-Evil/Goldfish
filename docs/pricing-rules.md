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

Rule changes go through the **pricing rules admin app** (`src/Goldfish.PricingAdmin`). The sqlcmd
ticket-script path is retired: the `goldfish_pricing_publisher` login can no longer run
`pricing.PublishRuleSetVersion`, and the only route to publish is `rulesadmin.ApproveAndPublish`, called by
the app when a manager approves. The design is in
[designs/pricing-rules-admin-app.md](designs/pricing-rules-admin-app.md).

A version is still a **full copy** of the rule set. A change request starts as a copy of the live rules.

1.  **Analyst** (`Pricing.Analyst` role): start a change request on the home page. It gets a reference
    `GFA-…`, which becomes the version's `ChangeTicketRef`.
2.  Edit the rules. **Check changes** shows every problem the database would reject, and the Ratio
    (new multiplier / old) of each rule. A **new rule**, or a rule whose ratio is outside the ratio band
    (0.8–1.25 by default), needs a written reason. **Save revision** stores a new, unchangeable revision.
3.  **Submit for approval.** Submitting is refused while there are problems, missing reasons, no changes,
    or a newer published version.
4.  **Manager** (`Pricing.Manager` role) who did not create, edit or submit the request: review the
    field-level diff, the ratios and the reasons. Then either **Reject** with a reason, or tick the
    confirmation and **Approve and publish**. **The change is live the moment the approval commits.**
    No pricing change is tied to a date or time, so approve it when it should apply.

Each published version records `ApprovedBy` as `<approver object ID>|<approver name>`. The app keeps every
revision, submission, rejection, comment, withdrawal and approval in the `rulesadmin` tables, which no
login can update or delete.

### When a draft is out of date

If another version is published after a draft was started, the draft cannot be approved (the page says so).
An analyst presses **Restart from version N**, which replaces the draft's rules with the latest published
rules as a new revision, re-enters the change, and submits again. There is no automatic merge, so a change
published in between can never be undone silently. A submitted draft must be rejected or withdrawn first.

### Correcting a mistake

Published versions are never edited or deleted. To correct one, start a new change request (it copies the
mistaken live version), fix the values, and have a manager approve it. Quotes already priced by the mistaken
version keep their prices and show which version priced them.

### If the app is unavailable

There is no emergency publish path through a non-privileged login. A DBA with sysadmin can still run
`pricing.PublishRuleSetVersion` directly. If that is ever necessary, record it under a change ticket and
use a `ChangeTicketRef` that is **not** `GFA-…`, so the verify script reports it (see Monitoring).

### App errors

| Error | Meaning | What to do |
| --- | --- | --- |
| 51001 | Approver identity missing | The sign-in has no `oid` claim; check the app registration. |
| 51002 | Change request does not exist | Reload the list. |
| 51003 | Already published | Nothing to do. A retry of your own approval shows the published version. |
| 51004 | The reviewed revision is no longer the latest | Someone saved a new revision; review it again. |
| 51005 | The latest revision has not been submitted | The analyst must submit it. |
| 51006 | The approver created, edited or submitted the request | Another manager must approve it. |
| 51007 | A new or out-of-band rule has no reason | The analyst must add the reason. |
| 50003, or 2601 `UX_PricingRuleSetVersions_VersionNumber` | A newer version was published first | Restart the draft from the latest version. |

Errors 50001–50005, 547 and the other 2601 rows below come from the publish procedure. The app's form checks
catch them before submitting; if one reaches approval, the app shows it as "Publishing failed" and nothing is
published.
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

Any error rolls back the whole approval, so nothing is half-published and no approval is recorded without its version.

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

The admin app logs each approval: `Change request {ChangeRequestId} revision {RevisionId} published as version
{VersionNumber}, approved by {ApproverOid}` at Information, refusals at Warning, and failed publishes at Error.

Publishes made outside the app (after go-live, any row here other than versions published before the app
existed means someone published around the approval rules):

```sql
SELECT VersionNumber, ChangeTicketRef, ApprovedBy, PublishedOnUtc
FROM pricing.PricingRuleSetVersions
WHERE ChangeTicketRef NOT LIKE N'GFA-%'
ORDER BY VersionNumber;
```

`verify-permissions.sql` runs the same query as its second result.

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

## Admin app setup

*   **Entra ID app registration** (single tenant), owned by the team that runs the app:
    *   Platform: Web, redirect URI `https://<host>/signin-oidc`, front-channel logout
        `https://<host>/signout-callback-oidc`.
    *   App roles (allowed member type: users/groups): `Pricing.Analyst` and `Pricing.Manager`.
        Assign them to people or groups under **Enterprise applications → Users and groups**. One person may
        hold both; they still cannot approve a request they worked on.
    *   Set **Assignment required** so people without a role cannot sign in at all.
*   **Configuration** (`src/Goldfish.PricingAdmin`): `AzureAd:TenantId`, `AzureAd:ClientId`, and a client
    credential (`AzureAd:ClientCredentials`, a certificate in production), plus the connection string
    `ConnectionStrings:RulesAdmin` for the `goldfish_rules_admin` login. In development use user secrets:

    ```powershell
    dotnet user-secrets set "AzureAd:TenantId" "<tenant-guid>" --project src/Goldfish.PricingAdmin
    dotnet user-secrets set "AzureAd:ClientId" "<client-guid>" --project src/Goldfish.PricingAdmin
    ```

*   Sign-in lasts one hour without sliding renewal, so a removed role stops working within the hour.
*   Run the app on the internal network only, over HTTPS, as a single instance.

## Goldfish.Web setup

`src/Goldfish.Web` is a second front end, built with MVC, Razor Pages and TypeScript, and it sits alongside
the Blazor admin app. MVC controllers manage **policies** through Goldfish.Api. Razor Pages under
`/Changes` run the same **pricing change** workflow as the Blazor app, through the same handlers and
`rulesadmin.ApproveAndPublish`.

*   **Roles:** the same app registration steps as the admin app, plus a third app role, `Policies.Editor`,
    for creating and editing policies. Analysts and managers can view policies; only policy editors can
    change them. Only analysts and managers can open `/Changes`.
*   **API key:** create a key named, for example, `goldfish-web` with `policies:read`, `policies:write`
    and `pricing:read` (see API keys). Configure `GoldfishApi:BaseUrl` and `GoldfishApi:ApiKey`; the app
    refuses to start without them. The API records the key name as each policy's `ModifiedBy`. The web app
    logs the signed-in person's object ID for every policy create and update
    (`Policy {PolicyId} {Action} by {UserOid}`).
*   **Database:** the `ConnectionStrings:RulesAdmin` setting uses the same `goldfish_rules_admin` login as the
    admin app.
*   **TypeScript:** `Scripts/*.ts` compiles to `wwwroot/js` during `dotnet build`, through
    `Microsoft.TypeScript.MSBuild`. The output is ignored by git. The scripts only give earlier feedback:
    adding and removing rule rows, a live ratio and reason check, an approve button that waits for the
    confirmation, and a base-price change summary. The server repeats every check.
*   **Policy edits** send the policy's ETag. If someone else saved first, the page shows their values and
    asks you to save again, so nothing is overwritten silently.

## Rollout

1.  Apply the migrations (`dotnet ef database update`, run by the migrator login). `AddRulesAdmin` also drops
    the old `dbo.Products` sample table.
2.  Create the `goldfish_rules_admin` login (a gMSA or managed identity where available, otherwise a SQL
    login with its password in the secret store).
3.  The DBA runs [`db/scripts/grant-permissions.sql`](../db/scripts/grant-permissions.sql) with
    `ApiUser`, `RulesAdminUser` and `PublisherUser`, then
    [`db/scripts/verify-permissions.sql`](../db/scripts/verify-permissions.sql). The verify script must show
    no rows with a `Problem`. This revokes the old publisher login's rights.
4.  Register the app in Entra ID and assign roles (see Admin app setup).
5.  Deploy the API and the admin app.
6.  If no version exists yet, publish the first rule set through the app: an analyst starts a request (it
    starts empty), adds the rules with a reason each, submits, and a manager approves. Until then, quote
    requests return 503 `Pricing.NoActiveVersion`.
    [`db/tickets/0001-initial-rule-set.sql`](../db/tickets/0001-initial-rule-set.sql) is kept as a record of
    environments that were seeded before the app existed.

**Never run the `AddPolicyPricing` or `AddRulesAdmin` migration's `Down` in production once data exists.**
They drop the policy, rule, quote and change-request tables.

## Accepted risks

| Risk | Mitigation |
| --- | --- |
| A multiplier typo such as 15 instead of 1.5 passes the database. | The approval screen shows every ratio; a ratio outside the band needs a written reason, enforced by the form and again by `rulesadmin.ApproveAndPublish`. Within the band, only the manager's review catches it. |
| One manager's approval publishes immediately. | Approval needs an explicit confirmation, shows the version number it will create, and is refused for anyone who worked on the request. |
| The approver's identity comes from the app, which authenticates people with Entra ID; the database trusts the object ID the app sends. | The app login can publish only through the wrapper, which requires a stored, submitted revision by someone else, so a forged approval still leaves a matching draft, submission and approval trail. The login cannot insert approvals or change history. |
| db_owner, sysadmin, and migrator logins can still change pricing rows or the procedures. | The API and app logins cannot. `verify-permissions.sql` flags privileged roles and lists versions not published through the app. Quotes keep copies of the rule values they used. |
| Base-price edits through `PUT /api/policies` skip rule review. | Only `policies:write` keys can make them. Each edit records `ModifiedBy`, the history table keeps every earlier row, and `If-Match` prevents silent overwrites. |
| Change-request comments, rejections and withdrawals are written by the app login without database checks. | They do not publish anything. Only approvals, which the wrapper checks, change prices. |
