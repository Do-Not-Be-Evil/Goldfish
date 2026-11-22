/*
    Grants the Goldfish database permissions (designs: "Database permissions" in
    docs/designs/policy-pricing-rules.md and docs/designs/pricing-rules-admin-app.md).
    Run by a DBA after the migrations, in each environment:

        sqlcmd -S <server> -d <database> -E -i db/scripts/grant-permissions.sql ^
               -v ApiUser="goldfish_api" RulesAdminUser="goldfish_rules_admin" PublisherUser="goldfish_pricing_publisher"

    $(ApiUser) and $(RulesAdminUser) are database user names. Each is created FOR LOGIN only when a server
    login with the same name exists and the user does not.

    $(PublisherUser) is the retired ticket-script login. The script never creates it; if it exists, its rights are
    revoked and EXECUTE on the publish procedure is denied, so the rules admin app is the only route to publish.

    The script is one batch (no GO) and can be run again safely: GRANT, REVOKE and DENY are idempotent.

    The pricing and rulesadmin schemas and their objects must be owned by dbo, which ownership chaining requires:
    the rules admin login publishes only through rulesadmin.ApproveAndPublish.
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiUser)')
   AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(ApiUser)')
    CREATE USER [$(ApiUser)] FOR LOGIN [$(ApiUser)];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(RulesAdminUser)')
   AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(RulesAdminUser)')
    CREATE USER [$(RulesAdminUser)] FOR LOGIN [$(RulesAdminUser)];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiUser)')
    THROW 50100, N'API user not found and no login of that name exists.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(RulesAdminUser)')
    THROW 50102, N'Rules admin user not found and no login of that name exists.', 1;

-- API login: read rules, write quotes, maintain policies.
GRANT SELECT ON OBJECT::pricing.PricingRuleSetVersions TO [$(ApiUser)];
GRANT SELECT ON OBJECT::pricing.PricingRules TO [$(ApiUser)];
GRANT SELECT, INSERT ON OBJECT::pricing.Quotes TO [$(ApiUser)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Policies TO [$(ApiUser)];
GRANT SELECT ON OBJECT::dbo.PoliciesHistory TO [$(ApiUser)];

-- Rules admin login: read published rules, add (never change) change-request rows, publish only via the wrapper.
GRANT SELECT ON OBJECT::pricing.PricingRuleSetVersions TO [$(RulesAdminUser)];
GRANT SELECT ON OBJECT::pricing.PricingRules TO [$(RulesAdminUser)];
GRANT SELECT, INSERT ON OBJECT::rulesadmin.ChangeRequests TO [$(RulesAdminUser)];
GRANT SELECT, INSERT ON OBJECT::rulesadmin.Revisions TO [$(RulesAdminUser)];
GRANT SELECT, INSERT ON OBJECT::rulesadmin.RevisionRules TO [$(RulesAdminUser)];
GRANT SELECT, INSERT ON OBJECT::rulesadmin.ChangeRequestEvents TO [$(RulesAdminUser)];
GRANT SELECT ON OBJECT::rulesadmin.Approvals TO [$(RulesAdminUser)];
GRANT SELECT ON OBJECT::rulesadmin.RatioBand TO [$(RulesAdminUser)];
GRANT EXECUTE ON OBJECT::rulesadmin.ApproveAndPublish TO [$(RulesAdminUser)];
DENY INSERT ON OBJECT::rulesadmin.Approvals TO [$(RulesAdminUser)];
DENY INSERT ON OBJECT::rulesadmin.RatioBand TO [$(RulesAdminUser)];
DENY EXECUTE ON OBJECT::pricing.PublishRuleSetVersion TO [$(RulesAdminUser)];

-- Published rules, quotes and the change-request history are never edited in place.
DENY UPDATE, DELETE ON OBJECT::pricing.PricingRuleSetVersions TO [$(ApiUser)], [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::pricing.PricingRules TO [$(ApiUser)], [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::pricing.Quotes TO [$(ApiUser)], [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.ChangeRequests TO [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.Revisions TO [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.RevisionRules TO [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.ChangeRequestEvents TO [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.Approvals TO [$(RulesAdminUser)];
DENY UPDATE, DELETE ON OBJECT::rulesadmin.RatioBand TO [$(RulesAdminUser)];

-- Retired ticket-script login: remove its grants and block the publish procedure.
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(PublisherUser)')
BEGIN
    REVOKE EXECUTE ON OBJECT::pricing.PublishRuleSetVersion FROM [$(PublisherUser)];
    REVOKE EXECUTE, REFERENCES ON TYPE::pricing.PricingRuleInput FROM [$(PublisherUser)];
    DENY EXECUTE ON OBJECT::pricing.PublishRuleSetVersion TO [$(PublisherUser)];
    DENY UPDATE, DELETE, INSERT ON OBJECT::pricing.PricingRuleSetVersions TO [$(PublisherUser)];
    DENY UPDATE, DELETE, INSERT ON OBJECT::pricing.PricingRules TO [$(PublisherUser)];
    PRINT N'Publish rights of $(PublisherUser) revoked.';
END;

PRINT N'Permissions granted to $(ApiUser) and $(RulesAdminUser).';
