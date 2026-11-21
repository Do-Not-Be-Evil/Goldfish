/*
    Ticket 0001: initial pricing rule set (version 1).
    Built from db/templates/publish-rule-set-version.sql. Run with @DryRun = 1 first, then 0.
*/
SET NOCOUNT ON;

DECLARE @ChangeTicketRef   nvarchar(64)  = N'CHG-0001';
DECLARE @ApprovedBy        nvarchar(128) = N'name@example';
DECLARE @BaseVersionNumber int           = 0;   -- no version exists yet
DECLARE @DryRun            bit           = 0;

DECLARE @Rules pricing.PricingRuleInput;
INSERT INTO @Rules (Priority, Name, PolicyType, MinCoverageLimitExclusive, MaxCoverageLimitInclusive, Multiplier)
VALUES
    --  Priority  Name                  PolicyType     Min (exclusive)  Max (inclusive)  Multiplier
    (   10,       N'Large commercial',  N'Commercial', 1000000.00,      NULL,            1.5000 ),
    (   1000,     N'Default',           NULL,          NULL,            NULL,            1.0000 );  -- catch-all

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
