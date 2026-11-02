using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class BudgetQueryUseCaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingTarget_WithZeroOrMultipleMemberships_AsksForSelection(bool multiple)
    {
        var user = new User("兼任", 1, AccountRole.Admin);
        if (multiple) { user.AddGroup(10); user.AddGroup(11); }
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByDiscordUserIdAsync(1)).ReturnsAsync(user);
        var groups = new Mock<IGroupRepository>(MockBehavior.Strict);
        var query = new BudgetQueryUseCase(groups.Object, users.Object, CreateConfiguration(4));
        var exception = await Assert.ThrowsAsync<BudgetManagementBotSystem.Application.Exceptions.GroupSelectionRequiredException>(
            () => query.GetRemainingBudgetAsync(1, null));
        Assert.Contains("group-id", exception.Message);
        await Assert.ThrowsAsync<BudgetManagementBotSystem.Application.Exceptions.GroupSelectionRequiredException>(
            () => query.GetUsageHistoryAsync(1));
        groups.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task OneMembershipOrExplicitTarget_ResolvesRequestedGroup(int? selected)
    {
        var user = new User("兼任", 1, AccountRole.GroupLeader, 10);
        if (selected.HasValue) user.AddGroup(11);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByDiscordUserIdAsync(1)).ReturnsAsync(user);
        var group = new Group("対象班");
        typeof(Group).GetProperty("Id")!.SetValue(group, 10);
        var groups = new Mock<IGroupRepository>();
        groups.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(group);
        var query = new BudgetQueryUseCase(groups.Object, users.Object, CreateConfiguration(4));
        Assert.Equal(10, (await query.GetRemainingBudgetAsync(1, selected)).GroupId);
        await query.GetUsageHistoryAsync(1, groupId: selected);
        groups.Verify(r => r.GetByIdAsync(10), Times.Exactly(2));
    }

    [Fact]
    public async Task GetRemainingBudgetAsync_WithSpecifiedFiscalYear_UsesSpecifiedYear()
    {
        var discordUserId = 12345UL;
        var groupId = 1;
        var fiscalYearStartMonth = 4;
        var specifiedFiscalYear = 2030;

        var user = new User("Test User", discordUserId, AccountRole.Accountant);
        user.AddGroup(0);
        var group = new Group("Test Group");
        group.AddBudgetTransaction(new BudgetTransaction(true, 200_000m, new FiscalYear(specifiedFiscalYear, fiscalYearStartMonth)));
        group.CreateBudgetRequest(user, new Money(50_000m), new FiscalYear(specifiedFiscalYear, fiscalYearStartMonth), "approved", Array.Empty<string>());
        group.CreateBudgetRequest(user, new Money(25_000m), new FiscalYear(specifiedFiscalYear, fiscalYearStartMonth), "pending", Array.Empty<string>());
        group.Requests.First(r => r.Description == "approved").UpdateStatus(RequestStatus.Approved);
        group.AddBudgetTransaction(new BudgetTransaction(true, 100_000m, new FiscalYear(specifiedFiscalYear + 1, fiscalYearStartMonth)));

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(repo => repo.GetByIdAsync(groupId)).ReturnsAsync(group);

        var useCase = new BudgetQueryUseCase(mockGroupRepository.Object, mockUserRepository.Object, CreateConfiguration(fiscalYearStartMonth));

        var result = await useCase.GetRemainingBudgetAsync(discordUserId, groupId, specifiedFiscalYear);

        Assert.Equal(200_000m, result.ActualBalance);
        Assert.Equal(25_000m, result.PendingTotal);
        Assert.Equal(175_000m, result.AvailableAfterPending);
    }

    [Fact]
    public async Task GetRemainingBudgetAsync_WithOmittedFiscalYear_UsesCurrentFiscalYear()
    {
        var discordUserId = 12345UL;
        var groupId = 1;
        var fiscalYearStartMonth = 4;
        var expectedFiscalYear = GetCurrentFiscalYear(fiscalYearStartMonth);

        var user = new User("Test User", discordUserId, AccountRole.Accountant);
        user.AddGroup(0);
        var group = new Group("Test Group");
        group.AddBudgetTransaction(new BudgetTransaction(true, 200_000m, new FiscalYear(expectedFiscalYear, fiscalYearStartMonth)));
        group.CreateBudgetRequest(user, new Money(20_000m), new FiscalYear(expectedFiscalYear, fiscalYearStartMonth), "approved", Array.Empty<string>());
        group.Requests.First(r => r.Description == "approved").UpdateStatus(RequestStatus.Approved);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(repo => repo.GetByIdAsync(groupId)).ReturnsAsync(group);

        var useCase = new BudgetQueryUseCase(mockGroupRepository.Object, mockUserRepository.Object, CreateConfiguration(fiscalYearStartMonth));

        var result = await useCase.GetRemainingBudgetAsync(discordUserId, groupId);

        Assert.Equal(200_000m, result.ActualBalance);
        Assert.Equal(0m, result.PendingTotal);
        Assert.Equal(200_000m, result.AvailableAfterPending);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_WithSpecifiedFiscalYear_FiltersByYear()
    {
        var discordUserId = 12345UL;
        var groupId = 1;
        var fiscalYearStartMonth = 4;
        var specifiedFiscalYear = 2030;

        var user = new User("Test User", discordUserId, AccountRole.Accountant);
        user.AddGroup(0);
        var group = new Group("Test Group");
        group.AddBudgetTransaction(new BudgetTransaction(true, 100_000m, new FiscalYear(specifiedFiscalYear, fiscalYearStartMonth)));
        group.AddBudgetTransaction(new BudgetTransaction(false, 20_000m, new FiscalYear(specifiedFiscalYear, fiscalYearStartMonth)));
        group.AddBudgetTransaction(new BudgetTransaction(true, 80_000m, new FiscalYear(specifiedFiscalYear + 1, fiscalYearStartMonth)));

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(repo => repo.GetByIdAsync(groupId)).ReturnsAsync(group);

        var useCase = new BudgetQueryUseCase(mockGroupRepository.Object, mockUserRepository.Object, CreateConfiguration(fiscalYearStartMonth));

        var result = await useCase.GetUsageHistoryAsync(discordUserId, page: 1, pageSize: 10, groupId: groupId, fiscalYear: specifiedFiscalYear);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items!, item => Assert.Equal(specifiedFiscalYear, item.FiscalYear));
    }

    private static IConfiguration CreateConfiguration(int fiscalYearStartMonth)
    {
        var settings = new Dictionary<string, string?>
        {
            ["FiscalYearStartMonth:Month"] = fiscalYearStartMonth.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static int GetCurrentFiscalYear(int fiscalYearStartMonth)
    {
        var currentYear = DateTime.Now.Year;
        return DateTime.Now.Month < fiscalYearStartMonth ? currentYear - 1 : currentYear;
    }
}
