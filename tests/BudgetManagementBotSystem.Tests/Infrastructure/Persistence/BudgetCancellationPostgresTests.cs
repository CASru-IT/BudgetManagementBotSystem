using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetManagementBotSystem.Tests.Infrastructure.Persistence;

public class BudgetCancellationPostgresTests
{
    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpgradesTwice_PreservesHistoryAndRejectsConcurrentWrites(bool legacySchema)
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BUDGET_TEST_POSTGRES"));
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        var database = "budget_cancel_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            settings.Database = database;
            settings.Pooling = false;
            var options = new DbContextOptionsBuilder<BudgetManagementDbContext>().UseNpgsql(settings.ConnectionString).Options;
            int groupId, transactionId;
            await using (var seed = new BudgetManagementDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var group = new Group("ハリボテ班");
                group.AddBudgetTransaction(new BudgetTransaction(true, 30000, new FiscalYear(2025, 4)));
                var extra = new BudgetTransaction(true, 5000, new FiscalYear(2025, 4));
                group.AddBudgetTransaction(extra);
                seed.Groups.Add(group);
                await seed.SaveChangesAsync();
                groupId = group.Id;
                transactionId = extra.Id;
                seed.Users.Add(new User("会計", 100, AccountRole.Accountant, groupId));
                await seed.SaveChangesAsync();
                // Recreate the pre-feature schema, retaining all existing business rows.
                if (legacySchema) await seed.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "Groups" DROP COLUMN "BudgetRevision";
                    ALTER TABLE "BudgetTransactions" DROP COLUMN "Kind";
                    ALTER TABLE "BudgetTransactions" DROP COLUMN "ReversesTransactionId" CASCADE;
                    ALTER TABLE "BudgetTransactions" DROP COLUMN "CancelledByUserId" CASCADE;
                    ALTER TABLE "BudgetTransactions" DROP COLUMN "CancellationReason";
                    """);
            }
            await using (var upgraded = new BudgetManagementDbContext(options))
            {
                await BudgetCancellationSchemaUpgrade.ApplyAsync(upgraded);
                await BudgetCancellationSchemaUpgrade.ApplyAsync(upgraded);
                // Old bot versions omit the new columns. They must still be able to write after rollback.
                await using (var oldWriter = await upgraded.Database.BeginTransactionAsync())
                {
                    await upgraded.Database.ExecuteSqlRawAsync("""
                        INSERT INTO "Groups" ("Name") VALUES ('旧バージョン形式の班');
                        INSERT INTO "BudgetTransactions" ("IsIncome", "Amount", "FiscalYear_Year", "FiscalYear_StartMonth", "TransactionDate", "GroupId")
                        SELECT TRUE, 100, 2025, 4, CURRENT_TIMESTAMP, "Id" FROM "Groups" WHERE "Name" = '旧バージョン形式の班';
                        """);
                    await oldWriter.RollbackAsync();
                }
                var group = (await new EfCoreGroupRepository(upgraded).GetByIdAsync(groupId))!;
                Assert.Equal(35000, group.GetTotalBudgetForFiscalYear(new FiscalYear(2025, 4)));
                Assert.All(group.BudgetTransactions, t => Assert.Equal(BudgetTransactionKind.Legacy, t.Kind));
            }
            await using var first = new BudgetManagementDbContext(options);
            await using var second = new BudgetManagementDbContext(options);
            var one = (await new EfCoreGroupRepository(first).GetByIdAsync(groupId))!;
            var two = (await new EfCoreGroupRepository(second).GetByIdAsync(groupId))!;
            var actor = (await first.Users.SingleAsync()).Id;
            one.CancelBudgetAddition(transactionId, actor, "班を間違えた");
            two.AddBudgetTransaction(new BudgetTransaction(false, 31000, new FiscalYear(2025, 4)));
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            await using var verify = new BudgetManagementDbContext(options);
            Assert.Equal(3, await verify.BudgetTransactions.CountAsync());
            Assert.Equal(30000, (await new EfCoreGroupRepository(verify).GetByIdAsync(groupId))!.GetTotalBudgetForFiscalYear(new FiscalYear(2025, 4)));
            var uc = new CancelBudgetAdditionUseCase(new EfCoreGroupRepository(verify), new EfCoreUserRepository(verify), new EfUnitOfWork(verify));
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => uc.PreviewAsync(100, transactionId, "二回目"));
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => verify.Database.ExecuteSqlRawAsync("""
                INSERT INTO "BudgetTransactions" ("IsIncome", "Amount", "FiscalYear_Year", "FiscalYear_StartMonth", "TransactionDate", "GroupId", "Kind", "ReversesTransactionId", "CancelledByUserId", "CancellationReason")
                SELECT "IsIncome", "Amount", "FiscalYear_Year", "FiscalYear_StartMonth", "TransactionDate", "GroupId", "Kind", "ReversesTransactionId", "CancelledByUserId", "CancellationReason"
                FROM "BudgetTransactions" WHERE "ReversesTransactionId" IS NOT NULL;
                """));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUDGET_TEST_POSTGRES")))
            Skip = "Set BUDGET_TEST_POSTGRES to an isolated PostgreSQL instance with CREATE DATABASE permission.";
    }
}
