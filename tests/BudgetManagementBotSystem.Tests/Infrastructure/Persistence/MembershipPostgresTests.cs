using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.Groups;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;

namespace BudgetManagementBotSystem.Tests.Infrastructure.Persistence;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUDGET_TEST_DB")))
            Skip = "Set BUDGET_TEST_DB to an empty disposable PostgreSQL database ending in _test.";
    }
}

public class MembershipPostgresTests
{
    [PostgresFact]
    public async Task LegacyUpgrade_ThenMultipleMemberships_PersistWithoutLosingFinancialData()
    {
        var connection = Environment.GetEnvironmentVariable("BUDGET_TEST_DB")!;
        Assert.EndsWith("_test", new NpgsqlConnectionStringBuilder(connection).Database);
        var options = new DbContextOptionsBuilder<BudgetManagementDbContext>().UseNpgsql(connection).Options;
        await using var db = new BudgetManagementDbContext(options);
        // Refuse an existing database: this fixture only owns a fresh disposable database.
        Assert.True(await db.Database.EnsureCreatedAsync());
        await MembershipSchemaUpgrade.ApplyAsync(db);
        Assert.Empty(await db.UserGroupMemberships.ToListAsync());
        var first = new Group("既存班");
        var second = new Group("兼任班");
        var emptyGroup = new Group("解除確認班");
        foreach (var g in new[] { first, second })
            g.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
        db.Groups.AddRange(first, second, emptyGroup);
        await db.SaveChangesAsync();
        var user = new User("利用者", 123, AccountRole.Admin, first.Id);
        var unassigned = new User("未所属", 456, AccountRole.GroupLeader);
        db.Users.AddRange(user, unassigned);
        await db.SaveChangesAsync();
        first.CreateBudgetRequest(user, new Money(50), new FiscalYear(4), "移行前の申請", new[] { "old/receipt.pdf" });
        await db.SaveChangesAsync();
        var userId = user.Id;
        db.ChangeTracker.Clear();
        // The previous schema has the same financial tables and only Users.GroupId.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"UserGroupMemberships\"");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__CasruSchemaUpdates\"");
        var before = await FinancialSnapshot(db);

        await MembershipSchemaUpgrade.ApplyAsync(db);
        await MembershipSchemaUpgrade.ApplyAsync(db);
        Assert.Equal(before, await FinancialSnapshot(db));
        Assert.Single(await db.UserGroupMemberships.ToListAsync());
        var users = new EfCoreUserRepository(db);
        var groups = new EfCoreGroupRepository(db);
        var commands = new UserCommandUseCase(users, groups, new EfUnitOfWork(db));
        Assert.Equal(new[] { first.Id }, (await users.GetByIdAsync(userId))!.GroupIds);
        Assert.Empty((await users.GetByDiscordUserIdAsync(456))!.GroupIds);

        await commands.AssignGroupByUserIdAsync(userId, second.Id);
        await commands.AssignGroupByDiscordIdAsync(123, second.Id);
        db.ChangeTracker.Clear();
        Assert.Equal(new[] { first.Id, second.Id }, (await users.GetByIdAsync(userId))!.GroupIds);
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await users.GetByDiscordUserIdAsync(123))!.Memberships.Count);
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await users.GetAllAsync())!.Single(u => u.Id == userId).Memberships.Count);
        var members = new UserQueryUseCase(users);
        Assert.Contains(await members.GetMembersByGroupIdAsync(first.Id), u => u.Id == userId);
        Assert.Contains(await members.GetMembersByGroupIdAsync(second.Id), u => u.Id == userId);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"UserGroupMemberships\" (\"UserId\", \"GroupId\") VALUES ({userId}, {second.Id})"));

        var files = new Mock<IFileStorage>();
        files.Setup(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>())).ReturnsAsync("test/receipt.pdf");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FiscalYearStartMonth:Month"] = "4" }).Build();
        var submit = new SubmitBudgetRequestUseCase(users, groups, config, new EfUnitOfWork(db), files.Object);
        foreach (var groupId in new[] { first.Id, second.Id })
            await submit.ExecuteAsync(userId, groupId, 100, "兼任班への申請", new[] { new UploadedEvidenceDto("receipt.pdf", new byte[] { 1 }) });
        var afterRequests = await FinancialSnapshot(db);
        Assert.Equal(3, await db.BudgetRequests.CountAsync());
        Assert.Equal(3, await db.RequestEvidences.CountAsync());

        await commands.UnassignGroupByUserIdAsync(userId, first.Id);
        db.ChangeTracker.Clear();
        await MembershipSchemaUpgrade.ApplyAsync(db);
        Assert.Equal(new[] { second.Id }, (await users.GetByIdAsync(userId))!.GroupIds);
        Assert.Equal(second.Id, await db.Database.SqlQuery<int>($"SELECT \"GroupId\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {userId}").SingleAsync());
        await Assert.ThrowsAsync<BudgetRequestMembershipException>(() => submit.ExecuteAsync(userId, first.Id, 10, "解除後", Array.Empty<UploadedEvidenceDto>()));
        Assert.Equal(afterRequests, await FinancialSnapshot(db));

        // Even a stale legacy column must never restore an intentionally removed membership.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Users\" SET \"GroupId\" = {first.Id} WHERE \"Id\" = {userId}");
        db.ChangeTracker.Clear();
        await MembershipSchemaUpgrade.ApplyAsync(db);
        Assert.Equal(new[] { second.Id }, (await users.GetByIdAsync(userId))!.GroupIds);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Users\" SET \"GroupId\" = {second.Id} WHERE \"Id\" = {userId}");
        db.ChangeTracker.Clear();

        await commands.AssignGroupByUserIdAsync(userId, emptyGroup.Id);
        var anotherUser = new User("削除対象班を主所属とする利用者", 789, AccountRole.GroupLeader, emptyGroup.Id);
        anotherUser.AddGroup(second.Id);
        await users.AddAsync(anotherUser);
        await db.SaveChangesAsync();
        await new DeleteGroupUseCase(groups, users, new EfUnitOfWork(db)).ExecuteAsync(123, emptyGroup.Id);
        db.ChangeTracker.Clear();
        Assert.Equal(new[] { second.Id }, (await users.GetByIdAsync(userId))!.GroupIds);
        Assert.Equal(new[] { second.Id }, (await users.GetByDiscordUserIdAsync(789))!.GroupIds);
        await users.DeleteAsync(anotherUser.Id);
        await db.SaveChangesAsync();
        await commands.UnassignGroupByDiscordIdAsync(123, second.Id);
        db.ChangeTracker.Clear();
        Assert.Empty((await users.GetByIdAsync(userId))!.Memberships);
        Assert.Null(await db.Database.SqlQuery<int?>($"SELECT \"GroupId\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {userId}").SingleAsync());
        await MembershipSchemaUpgrade.ApplyAsync(db);
        Assert.Empty(await db.UserGroupMemberships.ToListAsync());
        Assert.Equal(afterRequests, await FinancialSnapshot(db));
    }

    private static async Task<string> FinancialSnapshot(BudgetManagementDbContext db)
    {
        var parts = new List<string>();
        // Table identifiers below are a fixed allowlist, never user input.
#pragma warning disable EF1002
        foreach (var table in new[] { "BudgetRequests", "BudgetTransactions", "RequestEvidences", "RequestStatusChanges" })
            parts.Add(await db.Database.SqlQueryRaw<string>(
                $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY t.\"Id\"), '[]'::jsonb)::text AS \"Value\" FROM \"{table}\" t").SingleAsync());
#pragma warning restore EF1002
        return string.Join("\n", parts);
    }
}
