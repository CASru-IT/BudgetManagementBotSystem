using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Infrastructure.Persistence;

public static class BudgetCancellationSchemaUpgrade
{
    // EnsureCreated does not add columns to an existing database. Run before accepting commands.
    public static async Task ApplyAsync(BudgetManagementDbContext db)
    {
        if (!db.Database.IsNpgsql()) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Groups" ADD COLUMN IF NOT EXISTS "BudgetRevision" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "BudgetTransactions" ADD COLUMN IF NOT EXISTS "Kind" integer NOT NULL DEFAULT 0;
            ALTER TABLE "BudgetTransactions" ADD COLUMN IF NOT EXISTS "ReversesTransactionId" integer;
            ALTER TABLE "BudgetTransactions" ADD COLUMN IF NOT EXISTS "CancelledByUserId" integer;
            ALTER TABLE "BudgetTransactions" ADD COLUMN IF NOT EXISTS "CancellationReason" character varying(500);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BudgetTransactions_ReversesTransactionId" ON "BudgetTransactions" ("ReversesTransactionId");
            CREATE INDEX IF NOT EXISTS "IX_BudgetTransactions_CancelledByUserId" ON "BudgetTransactions" ("CancelledByUserId");
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BudgetTransactions_BudgetTransactions_ReversesTransactionId' AND conrelid = '"BudgetTransactions"'::regclass) THEN
                    ALTER TABLE "BudgetTransactions" ADD CONSTRAINT "FK_BudgetTransactions_BudgetTransactions_ReversesTransactionId"
                        FOREIGN KEY ("ReversesTransactionId") REFERENCES "BudgetTransactions" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BudgetTransactions_Users_CancelledByUserId' AND conrelid = '"BudgetTransactions"'::regclass) THEN
                    ALTER TABLE "BudgetTransactions" ADD CONSTRAINT "FK_BudgetTransactions_Users_CancelledByUserId"
                        FOREIGN KEY ("CancelledByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
            """);
        await transaction.CommitAsync();
    }
}
