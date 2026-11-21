/*
    Grants the Goldfish pricing permissions (design: "Database permissions").
    Run by a DBA after the AddPolicyPricing migration, in each environment:

        sqlcmd -S <server> -d <database> -E -i db/scripts/grant-permissions.sql ^
               -v ApiUser="goldfish_api" PublisherUser="goldfish_pricing_publisher"

    $(ApiUser) and $(PublisherUser) are database user names. Each user is created FOR LOGIN only when a
    server login with the same name exists and the user does not. The script is one batch (no GO) and can be
    run again safely: GRANT and DENY are idempotent.

    The pricing schema and all its objects must be owned by dbo, which ownership chaining requires: the
    publisher can insert rule rows only through pricing.PublishRuleSetVersion.
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiUser)')
   AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(ApiUser)')
    CREATE USER [$(ApiUser)] FOR LOGIN [$(ApiUser)];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(PublisherUser)')
   AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(PublisherUser)')
    CREATE USER [$(PublisherUser)] FOR LOGIN [$(PublisherUser)];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiUser)')
    THROW 50100, N'API user not found and no login of that name exists.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(PublisherUser)')
    THROW 50101, N'Publisher user not found and no login of that name exists.', 1;

-- API login: read rules, write quotes, maintain policies, and the existing Products sample.
GRANT SELECT ON OBJECT::pricing.PricingRuleSetVersions TO [$(ApiUser)];
GRANT SELECT ON OBJECT::pricing.PricingRules TO [$(ApiUser)];
GRANT SELECT, INSERT ON OBJECT::pricing.Quotes TO [$(ApiUser)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Policies TO [$(ApiUser)];
GRANT SELECT ON OBJECT::dbo.PoliciesHistory TO [$(ApiUser)];
GRANT SELECT, INSERT ON OBJECT::dbo.Products TO [$(ApiUser)];

-- Ticket-script login: publish through the procedure only.
GRANT EXECUTE ON OBJECT::pricing.PublishRuleSetVersion TO [$(PublisherUser)];
GRANT EXECUTE, REFERENCES ON TYPE::pricing.PricingRuleInput TO [$(PublisherUser)];

-- Published rules and quotes are never edited in place (G5).
DENY UPDATE, DELETE ON OBJECT::pricing.PricingRuleSetVersions TO [$(ApiUser)], [$(PublisherUser)];
DENY UPDATE, DELETE ON OBJECT::pricing.PricingRules TO [$(ApiUser)], [$(PublisherUser)];
DENY UPDATE, DELETE ON OBJECT::pricing.Quotes TO [$(ApiUser)], [$(PublisherUser)];

PRINT N'Pricing permissions granted to $(ApiUser) and $(PublisherUser).';
