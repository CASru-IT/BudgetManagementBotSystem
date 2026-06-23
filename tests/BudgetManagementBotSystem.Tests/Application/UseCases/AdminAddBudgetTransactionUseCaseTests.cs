using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class AdminAddBudgetTransactionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithAdminAndIncome_AddsIncomeTransaction()
    {
        const ulong discordUserId = 12345UL;
        const int groupId = 1;
        var admin = new User("Admin", discordUserId, AccountRole.Admin);
        var group = new Group("Test Group");
        typeof(Group).GetProperty("Id")!.SetValue(group, groupId);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repository => repository.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(admin);
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(repository => repository.GetByIdAsync(groupId)).ReturnsAsync(group);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new AdminAddBudgetTransactionUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            CreateConfiguration(),
            mockUnitOfWork.Object);

        var result = await useCase.ExecuteAsync(discordUserId, groupId, "income", 100_000m, 2030);

        var transaction = group.BudgetTransactions.Single();
        Assert.True(transaction.IsIncome);
        Assert.Equal(100_000m, transaction.Amount.Value);
        Assert.Equal(2030, transaction.FiscalYear.Year);
        Assert.Equal(100_000m, result.ActualBalance);
        mockUnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonAdmin_ThrowsUnauthorizedAccessException()
    {
        const ulong discordUserId = 12345UL;
        var user = new User("User", discordUserId, AccountRole.Accountant);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repository => repository.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);
        var mockGroupRepository = new Mock<IGroupRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new AdminAddBudgetTransactionUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            CreateConfiguration(),
            mockUnitOfWork.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => useCase.ExecuteAsync(discordUserId, 1, "income", 100_000m));

        mockGroupRepository.Verify(repository => repository.GetByIdAsync(It.IsAny<int>()), Times.Never);
        mockUnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpenseThatMakesNegative_ThrowsInvalidOperationException()
    {
        const ulong discordUserId = 12345UL;
        const int groupId = 1;
        var admin = new User("Admin", discordUserId, AccountRole.Admin);
        var group = new Group("Test Group");
        typeof(Group).GetProperty("Id")!.SetValue(group, groupId);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repository => repository.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(admin);
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(repository => repository.GetByIdAsync(groupId)).ReturnsAsync(group);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new AdminAddBudgetTransactionUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            CreateConfiguration(),
            mockUnitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(discordUserId, groupId, "expense", 10_000m, 2030));

        mockUnitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FiscalYearStartMonth:Month"] = "4"
            })
            .Build();
    }
}
