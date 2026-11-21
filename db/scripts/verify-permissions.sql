/*
    Read-only check of the Goldfish pricing permissions. Any row with a non-NULL Problem needs attention.

        sqlcmd -S <server> -d <database> -E -i db/scripts/verify-permissions.sql ^
               -v ApiUser="goldfish_api" PublisherUser="goldfish_pricing_publisher"
*/
SET NOCOUNT ON;

SELECT
    pr.name COLLATE DATABASE_DEFAULT                 AS UserName,
    p.state_desc COLLATE DATABASE_DEFAULT            AS State,
    p.permission_name COLLATE DATABASE_DEFAULT       AS Permission,
    p.class_desc COLLATE DATABASE_DEFAULT            AS ObjectClass,
    COALESCE(SCHEMA_NAME(o.schema_id) + N'.' + o.name,
             SCHEMA_NAME(t.schema_id) + N'.' + t.name) COLLATE DATABASE_DEFAULT AS ObjectName,
    CASE
        WHEN p.state_desc LIKE N'GRANT%'
         AND p.permission_name IN (N'UPDATE', N'DELETE')
         AND COALESCE(SCHEMA_NAME(o.schema_id), N'') = N'pricing'
        THEN N'UPDATE/DELETE granted'
    END COLLATE DATABASE_DEFAULT                     AS Problem
FROM sys.database_permissions p
JOIN sys.database_principals pr ON pr.principal_id = p.grantee_principal_id
LEFT JOIN sys.objects o ON p.class = 1 AND o.object_id = p.major_id
LEFT JOIN sys.types t ON p.class = 6 AND t.user_type_id = p.major_id
WHERE pr.name IN (N'$(ApiUser)', N'$(PublisherUser)')
  AND p.permission_name <> N'CONNECT'

UNION ALL

SELECT
    m.name COLLATE DATABASE_DEFAULT, N'MEMBER', r.name COLLATE DATABASE_DEFAULT, N'DATABASE_ROLE', NULL, N'privileged role'
FROM sys.database_role_members rm
JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
WHERE m.name IN (N'$(ApiUser)', N'$(PublisherUser)')
  AND r.name IN (N'db_owner', N'db_datawriter', N'db_ddladmin')

ORDER BY UserName, ObjectName, Permission;
