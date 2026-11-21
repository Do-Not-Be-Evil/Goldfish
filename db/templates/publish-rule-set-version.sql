/*
    Change-ticket template: publish a new pricing rule set version.

    1. Set @ChangeTicketRef and @ApprovedBy from the ticket.
    2. Set @BaseVersionNumber to the version this script was built from. Check it against
       GET /api/pricing/active-version (or SELECT MAX(VersionNumber) FROM pricing.PricingRuleSetVersions).
       If another version is published first, this script fails with error 50003: rebuild it from the new version.
    3. List EVERY rule in the new version (a version is a full copy), including exactly one catch-all
       (all three conditions NULL) with the largest priority number.
    4. Run with @DryRun = 1 first and attach the diff output to the ticket. The reviewer checks each
       Changed row's Ratio (for example 10.0000 means a multiplier went from 1.5 to 15).
    5. After approval, run with @DryRun = 0. The change is live the moment this script commits.

    Rules (lowest priority number wins; all conditions on a rule must be true):
      Priority                   int, 1 or more, unique in the version
      Name                       unique in the version; the dry run matches rules across versions by Name
      PolicyType                 'Commercial' or 'Personal' (exact case), or NULL for any type
      MinCoverageLimitExclusive  coverage limit must be ABOVE this value, or NULL for no lower limit
      MaxCoverageLimitInclusive  coverage limit must be AT MOST this value, or NULL for no upper limit
      Multiplier                 above 0; final price = base price x multiplier, rounded half-to-even to 2 places

    Errors are listed in docs/pricing-rules.md ("Procedure errors").
*/
SET NOCOUNT ON;

DECLARE @ChangeTicketRef   nvarchar(64)  = N'CHG-0000';
DECLARE @ApprovedBy        nvarchar(128) = N'name@example';
DECLARE @BaseVersionNumber int           = 0;
DECLARE @DryRun            bit           = 1;   -- set to 0 only after the ticket is approved

DECLARE @Rules pricing.PricingRuleInput;
INSERT INTO @Rules (Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
VALUES
    --  Priority  Name                 PolicyType     Min (exclusive)  Max (inclusive)  Multiplier
    (   10,       N'Example rule',     N'Commercial', 1000000.00,      NULL,            1.5000 ),
    (   1000,     N'Default',          NULL,          NULL,            NULL,            1.0000 );  -- catch-all

DECLARE @NewVersionNumber int;
EXEC pricing.PublishRuleSetVersion
    @ChangeTicketRef   = @ChangeTicketRef,
    @ApprovedBy        = @ApprovedBy,
    @BaseVersionNumber = @BaseVersionNumber,
    @DryRun            = @DryRun,
    @Rules             = @Rules,
    @NewVersionNumber  = @NewVersionNumber OUTPUT;

SELECT @NewVersionNumber AS NewVersionNumber,
       CASE WHEN @DryRun = 1 THEN N'Dry run: nothing was published.' ELSE N'Published.' END AS Outcome;
