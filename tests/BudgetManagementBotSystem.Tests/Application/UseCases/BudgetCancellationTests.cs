using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class BudgetCancellationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BudgetManagementDbContext> _options;
    private FiscalYear _year => new(2025, 4);
    private int _groupId;
    private int _additionId;

    public BudgetCancellationTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BudgetManagementDbContext>().UseSqlite(_connection).Options;
        using var db = Open();
        db.Database.EnsureCreated();
        var group = new Group("ハリボテ班");
        group.AddBudgetTransaction(new BudgetTransaction(true, 30000, _year));
        var addition = new BudgetTransaction(true, 5000, _year, BudgetTransactionKind.BudgetAddition);
        group.AddBudgetTransaction(addition);
        db.Groups.Add(group);
        db.SaveChanges();
        _groupId = group.Id;
        _additionId = addition.Id;
        db.Users.Add(new User("会計", 100, AccountRole.Accountant, group.Id));
        db.SaveChanges();
    }

    private BudgetManagementDbContext Open() => new(_options);
    private static CancelBudgetAdditionUseCase UseCase(BudgetManagementDbContext db) => new(
        new EfCoreGroupRepository(db), new EfCoreUserRepository(db), new EfUnitOfWork(db));

    [Fact]
    public async Task PreviewDoesNotWrite_ExecutePreservesOriginalAndAuditInOriginalYear()
    {
        using var db = Open();
        var uc = UseCase(db);
        var preview = await uc.PreviewAsync(100, _additionId, "  班を間違えた  ");
        Assert.Equal(35000, preview.BalanceBefore);
        Assert.Equal(30000, preview.BalanceAfter);
        Assert.Equal(2025, preview.FiscalYear);
        Assert.Equal(2, await db.BudgetTransactions.CountAsync());
        var result = await uc.ExecuteAsync(100, _additionId, "  班を間違えた  ", preview.Revision);
        Assert.Equal(30000, result.BalanceAfter);
        db.ChangeTracker.Clear();
        var group = await new EfCoreGroupRepository(db).GetByIdAsync(_groupId);
        Assert.Equal(30000, group!.GetTotalBudgetForFiscalYear(_year));
        Assert.Equal(0, group.GetTotalBudgetForFiscalYear(new FiscalYear(2026, 4)));
        Assert.Equal(5000, group.BudgetTransactions.Single(t => t.Id == _additionId).Amount.Value);
        var reversal = Assert.Single(group.BudgetTransactions, t => t.ReversesTransactionId == _additionId);
        Assert.False(reversal.IsIncome);
        Assert.Equal(BudgetTransactionKind.BudgetCancellation, reversal.Kind);
        Assert.Equal("班を間違えた", reversal.CancellationReason);
        Assert.Equal((await db.Users.SingleAsync()).Id, reversal.CancelledByUserId);
        Assert.True(reversal.TransactionDate > DateTime.UtcNow.AddMinutes(-1));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => uc.PreviewAsync(100, _additionId, "二回目"));
        Assert.Equal(3, await db.BudgetTransactions.CountAsync());
    }

    [Theory]
    [InlineData(AccountRole.Admin, true)]
    [InlineData(AccountRole.President, true)]
    [InlineData(AccountRole.Accountant, true)]
    [InlineData(AccountRole.GroupLeader, false)]
    public async Task OnlyAuthorizedActiveUsersCanCancel(AccountRole role, bool allowed)
    {
        using var db = Open();
        var user = await db.Users.SingleAsync();
        user.ChangeRole(role);
        await db.SaveChangesAsync();
        if (allowed) await UseCase(db).PreviewAsync(100, _additionId, "訂正");
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase(db).PreviewAsync(100, _additionId, "訂正"));
        user.Deactivate();
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase(db).PreviewAsync(100, _additionId, "訂正"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase(db).PreviewAsync(999, _additionId, "訂正"));
    }

    [Theory]
    [InlineData(30001, 0)]
    [InlineData(0, 30001)]
    [InlineData(20000, 10001)]
    public async Task RefusesToRemoveSpentOrCommittedBudget(decimal spent, decimal pending)
    {
        using var db = Open();
        var group = (await new EfCoreGroupRepository(db).GetByIdAsync(_groupId))!;
        if (spent > 0) group.AddBudgetTransaction(new BudgetTransaction(false, spent, _year));
        if (pending > 0) group.CreateBudgetRequest(await db.Users.SingleAsync(), new Money(pending), _year, "資材", []);
        await db.SaveChangesAsync();
        var count = await db.BudgetTransactions.CountAsync();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => UseCase(db).PreviewAsync(100, _additionId, "訂正"));
        Assert.Equal(count, await db.BudgetTransactions.CountAsync());
    }

    [Fact]
    public async Task AllowsExactBalanceAndIgnoresOtherFiscalYears()
    {
        using var db = Open();
        var group = (await new EfCoreGroupRepository(db).GetByIdAsync(_groupId))!;
        group.AddBudgetTransaction(new BudgetTransaction(false, 30000, _year));
        group.CreateBudgetRequest(await db.Users.SingleAsync(), new Money(99999), new FiscalYear(2026, 4), "別年度", []);
        await db.SaveChangesAsync();
        var p = await UseCase(db).PreviewAsync(100, _additionId, "訂正");
        await UseCase(db).ExecuteAsync(100, _additionId, "訂正", p.Revision);
        Assert.Equal(0, group.GetTotalBudgetForFiscalYear(_year));
    }

    [Fact]
    public async Task RejectsExpenseRefundAndMissingId_ButMarksLegacyIncome()
    {
        using var db = Open();
        var group = (await new EfCoreGroupRepository(db).GetByIdAsync(_groupId))!;
        var expense = new BudgetTransaction(false, 10, _year);
        var refund = new BudgetTransaction(true, 10, _year, BudgetTransactionKind.ApprovalRefund);
        group.AddBudgetTransaction(expense);
        group.AddBudgetTransaction(refund);
        await db.SaveChangesAsync();
        foreach (var id in new[] {expense.Id, refund.Id})
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => UseCase(db).PreviewAsync(100, id, "訂正"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => UseCase(db).PreviewAsync(100, 99999, "訂正"));
        var legacy = group.BudgetTransactions.Single(t => t.Amount.Value == 30000);
        Assert.True((await UseCase(db).PreviewAsync(100, legacy.Id, "訂正")).IsLegacy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsEmptyReason(string reason)
    {
        using var db = Open();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => UseCase(db).PreviewAsync(100, _additionId, reason));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => UseCase(db).PreviewAsync(100, _additionId, new string('x', 501)));
    }

    [Fact]
    public async Task ConfirmationRechecksPermissionAndRevision()
    {
        using var first = Open();
        var preview = await UseCase(first).PreviewAsync(100, _additionId, "訂正");
        using (var other = Open())
        {
            var group = (await new EfCoreGroupRepository(other).GetByIdAsync(_groupId))!;
            group.AddBudgetTransaction(new BudgetTransaction(false, 1, _year));
            await other.SaveChangesAsync();
        }
        using var confirm = Open();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => UseCase(confirm).ExecuteAsync(100, _additionId, "訂正", preview.Revision));
        var fresh = await UseCase(confirm).PreviewAsync(100, _additionId, "訂正");
        (await confirm.Users.SingleAsync()).Deactivate();
        await confirm.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase(confirm).ExecuteAsync(100, _additionId, "訂正", fresh.Revision));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentCancellationOrExpenseRollsBackCompletely(bool secondCancellation)
    {
        using var first = Open();
        using var second = Open();
        var one = (await new EfCoreGroupRepository(first).GetByIdAsync(_groupId))!;
        var two = (await new EfCoreGroupRepository(second).GetByIdAsync(_groupId))!;
        var actor = (await first.Users.SingleAsync()).Id;
        one.CancelBudgetAddition(_additionId, actor, "訂正");
        if (secondCancellation) two.CancelBudgetAddition(_additionId, actor, "重複操作");
        else two.AddBudgetTransaction(new BudgetTransaction(false, 31000, _year));
        await first.SaveChangesAsync();
        var conflict = await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());
        if (!secondCancellation) Assert.IsType<DbUpdateConcurrencyException>(conflict);
        using var verify = Open();
        Assert.Equal(3, await verify.BudgetTransactions.CountAsync());
        Assert.Equal(30000, (await new EfCoreGroupRepository(verify).GetByIdAsync(_groupId))!.GetTotalBudgetForFiscalYear(_year));
    }

    public void Dispose() => _connection.Dispose();
}
