# Stage 9: Implementation Readiness Test

* **Target:** `docs/designs/policy-pricing-rules.md`, DetailedRange (lines 791–1589)
* **Context:** `concepts_brief.md`. The Stage 8 report was given only as optional reference.
* **Standards:** egm-implementation-readiness and egm-codebase-details used the implementation-readiness standard. egm-style used its own checks.

| Reviewer | Status | Raw findings |
| --- | --- | --- |
| egm-implementation-readiness | completed | 1 Blocker, 9 Major, 10 Nit |
| egm-codebase-details | completed | 11 Major, 13 Nit |
| egm-style | completed | 11 Major, 12 Nit |

**After removing duplicates: 🔴 1 Blocker, 🟡 14 Major, 🟢 13 Nit.**

**Verdict:** the plan is not ready to implement. The core procedure contract is undefined, and the work isn't split into phases.

**What checked out:**
* Every path the plan marks "Modify" exists.
* No file it marks "Create" collides with an existing file.
* Every claim the plan makes about the current code is accurate: EF 10.0.15, `Result`/`Error`, the two inline `Problem(...)` calls, the inline UTC converter, `EnableRetryOnFailure`, `TimeProvider`, the migrations folder, and EF Design in the Api project.

**Correction from the main session:** M13 says there is no tool manifest for dotnet-ef. In fact `C:\Goldfish\dotnet-tools.json` already pins dotnet-ef 10.0.15. It sits at the repo root rather than in `.config/`, so the plan only needs to mention it.

## 🔴 Blocker

### B1. The publish procedure's contract is undefined

* **Raised by:** all three reviewers.
* **Lines:** 1143–1149, 1358–1368, 1388–1389, 1472–1497, 1576–1577.
* **What's missing from the plan:**
  * `pricing.PricingRuleInput` has no columns, types, nullability, or order.
  * `@ChangeTicketRef` and `@ApprovedBy` have no types, and nothing says whether blank values are rejected.
  * Nothing says how the new version number is returned: an OUTPUT parameter, a result set, or the return value.
* **The error numbering doesn't match the checks:**
  * The plan maps "THROW 50001–50005" onto steps 1–5, but only steps 1 and 2 are checks.
  * Most of the rejections that test 66 expects come from constraints. Those raise SQL errors 547, 2601, or 2627, not 5000x.
  * The runbook and the coverage gate both depend on this undefined list.
* **Why it matters:** this procedure is the only way rules get written. Files 34, 55, 56, 65, and 66 all depend on it.
* **Direction:**
  * Write out the full TVP DDL and the procedure signature, including how the version number is returned.
  * Add a table with one row per rejected condition: where it is enforced, the error number, and the message.
  * For each case in test 66, name the error it should assert.
* **If Stage 8 is adopted:** this grows to cover `@BaseVersionNumber` and `@DryRun`. Z9 makes the error table smaller.

## 🟡 Major

### M1. No phases or checkpoints

* **Raised by:** implementation-readiness, codebase-details.
* **Lines:** 791–1559.
* **Problem:** the plan is a flat list of 71 files. Some changes break other code until a later file lands. For example, changing `Error` breaks Products until files 2 and 44 are in place.
* **Direction:** split the work into phases, each with a build and test command:
  1. Error types and the shared Problem mapping.
  2. Policies.
  3. The pricing schema and the procedure.
  4. Matching and quotes.
  5. Authentication.
  6. Database scripts, permission tests, and the runbook.
* **If Stage 8 is adopted:** still needed.

### M2. The matching query's row can't be mapped to `MatchRow`

* **Raised by:** all three reviewers.
* **Lines:** 1177–1191.
* **Problem:**
  * `CASE ... 1 ELSE 0` returns an int, but `MatchRow` expects a bool.
  * The query returns `Name`, but the plan expects `RuleName`.
  * The columns from `OUTER APPLY` can be null, and `MatchRow` doesn't allow for that.
  * `MatchRow`'s properties are never listed.
  * The parameter placeholders don't fit `FormattableString`.
  * The plan says `SqlQuery`, but the spec says `SqlQueryRaw`.
* **Direction:**
  * Define `MatchRow` with nullable rule fields.
  * Use `CAST(... AS bit)`.
  * Write out the exact C# SQL text.
  * Use one API name in both places.
* **If Stage 8 is adopted:** goes away, because matching moves to C#.

### M3. The grant script is missing rights the plan itself needs

* **Raised by:** codebase-details, implementation-readiness.
* **Lines:** 1335–1346, 1549–1558.
* **Problem:**
  * The publisher has no `GRANT EXECUTE ON TYPE::pricing.PricingRuleInput`. Without it, the template and test 71 fail.
  * The API login gets no rights on `dbo.Products`. The existing Products endpoints break once the API connects as `goldfish_api`.
* **Direction:** add both grants to files 53 and 54, to test 71, and to the spec's permissions table. Alternatively, retire Products.
* **If Stage 8 is adopted:** still needed.

### M4. The test database override could silently hit the developer database

* **Raised by:** implementation-readiness, codebase-details.
* **Lines:** 1444–1470.
* **Problem:**
  * `AddInfrastructure` reads the connection string while services are being registered.
  * `appsettings.json` supplies a working fallback, the developer's own `Goldfish` database.
  * The plan never names how the test override works. A late `ConfigureAppConfiguration` would miss the read.
  * The test keys and their hashes aren't given.
* **Direction:**
  * Override with `UseSetting("ConnectionStrings:Database", ...)`, or replace `DbContextOptions` in `ConfigureTestServices`.
  * Guard that the database name starts with `Goldfish_Test_`.
  * List the test key values and their hashes.
* **If Stage 8 is adopted:** still needed.

### M5. The grant script's variables, and how the test runs the script, are undefined

* **Raised by:** all three reviewers.
* **Lines:** 1335–1346, 1545–1559.
* **Problem:**
  * The script uses sqlcmd `$(...)` variables, but the test runs it through ADO.NET. ADO.NET doesn't substitute those variables or split batches on `GO`.
  * It's unclear whether the variables are login names or user names.
  * The claim that the script can be run twice safely is never tested.
* **Direction:**
  * Make the variables user names.
  * Describe the test loader: replace the variables, split on `GO`, then run each batch. Or call `sqlcmd -v` instead.
  * Either run the script twice in the test, or reword the claim as an intention.
* **If Stage 8 is adopted:** still needed.

### M6. Test project setup and isolation are not decided

* **Raised by:** codebase-details, implementation-readiness.
* **Lines:** 1409–1478, 1499–1510.
* **Problem:**
  * Neither the xUnit version (v2 or v3) nor the test SDK versions are pinned.
  * `ResetAsync` has no stated timing and no delete order. The order must be Quotes, then Rules, then Versions, then Policies.
  * There's no shared serial collection.
  * Nothing says how the factory gets the fixture's connection string.
  * The `internal` types are unreachable from tests, because there's no `InternalsVisibleTo` and the Infrastructure csproj isn't in the file list.
* **Direction:** pin the versions, and write out the contract for the fixture, the collection, and resets. Tests should resolve services from `factory.Services.CreateScope()`.
* **If Stage 8 is adopted:** partly goes away. There's no history-table reset, and matching becomes unit-tested.

### M7. The metrics wiring won't compile, and package pins contradict each other

* **Raised by:** all three reviewers.
* **Lines:** 793–796, 931–937, 999, 1047–1055, 1439–1440.
* **Problem:**
  * `AddMetrics()` lives in `Microsoft.Extensions.Diagnostics`, not in the `.Abstractions` package the plan references. It's also unnecessary, because the web host already registers it.
  * Nothing exports the metric.
  * "Pinned exactly" contradicts the "10.0.x" versions given elsewhere.
* **Direction:**
  * Drop `AddMetrics()`.
  * Pin every package at 10.0.15.
  * Decide whether `Microsoft.Data.SqlClient` gets its own reference.
* **If Stage 8 is adopted:** the metrics part goes away, but the pinning still needs fixing.

### M8. Nothing generates `PricingRules.Id`

* **Raised by:** codebase-details, style.
* **Lines:** 1102–1133.
* **Problem:** the procedure's INSERT fails because the non-null primary key has no default.
* **Direction:** add `HasDefaultValueSql("NEWSEQUENTIALID()")`.
* **If Stage 8 is adopted:** still needed.

### M9. API key authentication details are left open

* **Raised by:** codebase-details, implementation-readiness.
* **Lines:** 1211–1245, 1275–1277.
* **Problems, each with its fix:**
  * Key names have no length limit, and a name over 64 characters overflows `RequestedBy`. Limit names to 1–64 characters.
  * Use `ClaimTypes.Name` for the name claim.
  * Create the identity with an authentication type, `"ApiKey"`. Without one, `IsAuthenticated` is false.
  * Make the bound options classes use `{ get; set; }` properties.
  * Initialize `Keys = new()`.
  * Compare hashes with `Convert.FromHexString`, ignoring case.
  * State how the handler gets its options: through `IOptionsMonitor`.
  * Handle the warning that `User.Identity!.Name` is nullable.
* **If Stage 8 is adopted:** still needed if API keys stay. Goes away if JWT is used.

### M10. The `READ_COMMITTED_SNAPSHOT` step is underspecified, and its stated reason is wrong

* **Raised by:** implementation-readiness, style.
* **Lines:** 1150–1155.
* **Problem:**
  * The step needs ALTER DATABASE permission.
  * Its position in `Up` isn't given, and there's no verification query.
  * Plain READ COMMITTED already prevents partial reads. RCSI's only benefit is that reads don't wait.
* **If Stage 8 is adopted:** goes away.

### M11. The order of the quote handler's steps and its logging is unclear

* **Raised by:** style, implementation-readiness, codebase-details.
* **Lines:** 1006–1015.
* **Problem:**
  * The log for `InvalidActiveVersion` sits after an early return, so it never runs.
  * Nothing says how the other errors are logged.
  * There are no message templates.
* **Direction:** split the steps into a success path and a failure path, and give a log level and template for each error code.
* **If Stage 8 is adopted:** still needed. The metric step goes, and the catch-all log becomes Information.

### M12. "When no version has started" brings back effective-date wording

* **Raised by:** style.
* **Lines:** 878, 1038.
* **Direction:** say "when no version has been published".
* **If Stage 8 is adopted:** still needed.

### M13. The Verification Plan gates can't be run as written

* **Raised by:** all three reviewers.
* **Lines:** 1139–1140, 1563–1583.
* **Problem:**
  * `TreatWarningsAsErrors` isn't in the csproj entries.
  * There's no coverage tool or command.
  * `dotnet ef database update` would target the developer database.
  * The CI pipeline the plan relies on doesn't exist.
  * The manual check never creates Policy P, and never says which login runs the script.
* **Direction:**
  * Add the property and `coverlet.collector`, along with the commands.
  * Add `--connection` to the database update.
  * Point to `dotnet-tools.json`.
  * Add a step that creates the CUJ 1 policy.
* **If Stage 8 is adopted:** still needed.

### M14. The procedure's concurrency rule conflicts with the Stage 8 direction

* **Raised by:** implementation-readiness.
* **Lines:** 1146–1148, 1365–1367, 1496.
* **Problem:** the plan uses `SERIALIZABLE` with MAX+1, with no base-version input and no concurrency test.
* **Direction:**
  * With Stage 8: add test cases for a stale base version and for two publishes racing on the same base.
  * Without Stage 8: add a test for concurrent publishes.

## 🟢 Nit

* **N1.** Define "more than 2 decimal places" precisely, because `100.250` is ambiguous. Give each validation failure an error code and an upper bound.
* **N2.** Match the existing convention `CancellationToken cancellationToken = default` (`IProductRepository.cs:5`).
* **N3.** `PolicyRepository.GetByIdAsync` tracks entities for every caller, but the existing convention is `AsNoTracking`. Goes away if Stage 8 removes PUT.
* **N4.** The temporal configuration has the wrong syntax: it should be `ToTable("Policies", tb => { tb.IsTemporal(...); tb.HasCheckConstraint(...); })`. Separately, "the test user is sysadmin" conflicts with the `api_test` user. The first part goes away under Stage 8.
* **N5.** Constraints:
  * Name every constraint.
  * Copy the UTC converter inline rather than extracting it.
  * Add a CHECK on `Quotes.PolicyType`.
  * Use `ValueGeneratedNever` on Quotes.
* **N6.** Under the default collation, the `PolicyType` CHECK also accepts `'commercial'`. This gets worse under C# matching unless the column uses `Latin1_General_BIN2`.
* **N7.** The property types of the persistence records, and their navigation and foreign-key names, are left open.
* **N8.** File 39 doesn't choose a query mechanism. Goes away under C# matching.
* **N9.** `public partial class Program;` is redundant on .NET 10 and triggers analyzer ASP0027 at info level. Confirm it doesn't break the 0-warnings gate.
* **N10.** File 52 sends keys over `http://localhost:5169`. Use `https://localhost:7258`.
* **N11.** The ticket template needs literal values for its parameters. The PowerShell one-liner for generating a key is promised but missing; it should produce 32 random bytes.
* **N12.** The return type of `UpdatePolicyHandler` and the PUT response body aren't defined. Goes away under Stage 8.
* **N13.** Wording:
  * Drop the redundant rounding clause.
  * Drop "deliberately", "a safe default", "fast", "very", and "always".
  * "Turns on G8" becomes "Enables authentication (G8)".
  * "Which decision 6 requires" overclaims.
  * The test-database decision is missing: add it as decision 13.
  * List the `reader` key's permissions.
  * Define what counts as a "flag" in the verify script.

## Effect of adopting Stage 8

* **Goes away:** M2, M10, N3, N8, N12, and the temporal part of N4.
* **Mostly goes away:**
  * M7: the metrics part goes; the pinning stays.
  * M6: the history-table reset and internals access go.
* **Changes shape:**
  * B1 gets larger, though Z9 makes its error table smaller.
  * M14 becomes the stale-base and race tests.
  * M11 loses its metric step.
* **Gets worse unless handled:** N6, without binary collation.
* **Not affected:** M1, M3, M4, M5, M8, M9 (unless JWT is chosen), M12, M13, N1, N2, N5, N7, N9, N10, N11, N13.
