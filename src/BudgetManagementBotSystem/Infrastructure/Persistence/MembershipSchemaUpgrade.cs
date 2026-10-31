using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Infrastructure.Persistence;

public static class MembershipSchemaUpgrade
{
    // Existing deployments predate EF migrations and use EnsureCreated. This additive,
    // transactional upgrade also supports freshly created databases. Never re-import
    // the legacy column after the migration has been recorded.
    public static async Task ApplyAsync(BudgetManagementDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            SELECT pg_advisory_xact_lock(20261031, 1);
            CREATE TABLE IF NOT EXISTS "__CasruSchemaUpdates" (
                "Version" text PRIMARY KEY,
                "AppliedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS "UserGroupMemberships" (
                "UserId" integer NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
                "GroupId" integer NOT NULL REFERENCES "Groups" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "PK_UserGroupMemberships" PRIMARY KEY ("UserId", "GroupId")
            );
            CREATE INDEX IF NOT EXISTS "IX_UserGroupMemberships_GroupId"
                ON "UserGroupMemberships" ("GroupId");
            INSERT INTO "UserGroupMemberships" ("UserId", "GroupId")
            SELECT "Id", "GroupId" FROM "Users"
            WHERE "GroupId" IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM "__CasruSchemaUpdates" WHERE "Version" = '20261031-multiple-groups'
            ) ON CONFLICT DO NOTHING;
            INSERT INTO "__CasruSchemaUpdates" ("Version") VALUES ('20261031-multiple-groups')
                ON CONFLICT DO NOTHING;
            """);
        await transaction.CommitAsync();
    }
}
