using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class BudgetCancellationCandidatesTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly User _admin = new("管理者", 100, AccountRole.Admin);
    private readonly List<Group> _data = [];
    private int _id;
    private CancelBudgetAdditionUseCase UseCase => new(_groups.Object, _users.Object, Mock.Of<IUnitOfWork>());

    public BudgetCancellationCandidatesTests()
    {
        _groups.Setup(x => x.GetAllAsync()).ReturnsAsync(_data);
        _users.Setup(x => x.GetByDiscordUserIdAsync(100)).ReturnsAsync(_admin);
    }

    private BudgetTransaction Add(Group group, decimal amount = 5000, bool income = true,
        BudgetTransactionKind kind = BudgetTransactionKind.BudgetAddition, int year = 2026)
    {
        var transaction = new BudgetTransaction(income, amount, new FiscalYear(year, 4), kind);
        typeof(BudgetTransaction).GetProperty("Id")!.SetValue(transaction, ++_id);
        typeof(BudgetTransaction).GetProperty("TransactionDate")!.SetValue(transaction, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(_id));
        group.AddBudgetTransaction(transaction);
        return transaction;
    }

    [Fact]
    public async Task ExcludesExpensesRefundsReversalsAlreadyCancelledAndInsufficientBudgets()
    {
        var group = new Group("ハリボテ班");
        _data.Add(group);
        var original = Add(group, 100000);
        var good = Add(group);
        var legacy = Add(group, kind: BudgetTransactionKind.Legacy);
        Add(group, 100, false, BudgetTransactionKind.Legacy);
        Add(group, 100, true, BudgetTransactionKind.ApprovalRefund);
        var cancelled = Add(group);
        var reversal = group.CancelBudgetAddition(cancelled.Id, 1, "訂正");
        typeof(BudgetTransaction).GetProperty("Id")!.SetValue(reversal, ++_id);
        var spent = new Group("使用済み班");
        Add(spent);
        Add(spent, 4900, false, BudgetTransactionKind.Legacy);
        _data.Add(spent);
        var candidates = await UseCase.GetCandidatesAsync(100, "");
        Assert.Equal(new[] {legacy.Id, good.Id, original.Id}, candidates.Select(x => x.TransactionId));
        Assert.True(candidates.Single(x => x.TransactionId == legacy.Id).IsLegacy);
        Assert.False(candidates.Single(x => x.TransactionId == good.Id).IsLegacy);
    }

    [Fact]
    public async Task SearchesAllHistoryBeforeLimitingToLatest25()
    {
        var oldGroup = new Group("ハリボテ班");
        _data.Add(oldGroup);
        var old = Add(oldGroup, 1234.50m, year: 2025);
        var busy = new Group("最近の班");
        _data.Add(busy);
        for (var i = 0; i < 30; i++) Add(busy);
        var latest = await UseCase.GetCandidatesAsync(100, "");
        Assert.Equal(25, latest.Count);
        Assert.Equal(31, latest[0].TransactionId);
        foreach (var search in new[] { "ハリボテ", "1234.50", "2025", "  ハリボテ  " })
            Assert.Equal(old.Id, Assert.Single(await UseCase.GetCandidatesAsync(100, search)).TransactionId);
        Assert.Contains(await UseCase.GetCandidatesAsync(100, "1"), c => c.TransactionId == old.Id);
        Assert.Empty(await UseCase.GetCandidatesAsync(100, "見つからない班"));
    }

    [Fact]
    public async Task PendingCommitmentsAlsoExcludeUnaffordableCancellation()
    {
        var group = new Group("申請中の班");
        Add(group);
        _admin.AddGroup(group.Id);
        group.CreateBudgetRequest(_admin, new Money(1), new FiscalYear(2026, 4), "資材", []);
        _data.Add(group);
        Assert.Empty(await UseCase.GetCandidatesAsync(100, ""));
    }

    [Theory]
    [InlineData(AccountRole.Admin)]
    [InlineData(AccountRole.President)]
    [InlineData(AccountRole.Accountant)]
    public async Task AuthorizedRolesSeeCandidatesButInactiveOrUnregisteredUsersDoNot(AccountRole role)
    {
        _admin.ChangeRole(role);
        var group = new Group("班");
        Add(group);
        _data.Add(group);
        Assert.Single(await UseCase.GetCandidatesAsync(100, ""));
        _admin.Deactivate();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase.GetCandidatesAsync(100, ""));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase.GetCandidatesAsync(999, ""));
        _admin.Activate();
        _admin.ChangeRole(AccountRole.GroupLeader);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UseCase.GetCandidatesAsync(100, ""));
    }
}
