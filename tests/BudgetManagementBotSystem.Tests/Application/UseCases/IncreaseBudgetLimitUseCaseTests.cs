using Moq;
using Microsoft.Extensions.Configuration;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class IncreaseBudgetLimitUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidGroupAndAmount_AddsBudgetTransaction()
    {
        // Arrange
        var groupId = 1;
        var amount = 100000m;
        var group = new Group("Test Group");

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository
            .Setup(repo => repo.GetByIdAsync(groupId))
            .ReturnsAsync(group);

        var configuration = CreateConfiguration(4);

        var useCase = new IncreaseBudgetLimitUseCase(mockGroupRepository.Object, configuration);

        // Act
        await useCase.ExecuteAsync(groupId, amount);

        // Assert
        Assert.Single(group.BudgetTransactions);
        var transaction = group.BudgetTransactions.First();
        Assert.True(transaction.IsIncome);
        Assert.Equal(amount, transaction.Amount.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullGroup_ThrowsArgumentNullException()
    {
        // Arrange
        var groupId = 999;
        var amount = 100000m;

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository
            .Setup(repo => repo.GetByIdAsync(groupId))
            .ReturnsAsync((Group?)null);

        var configuration = CreateConfiguration(4);

        var useCase = new IncreaseBudgetLimitUseCase(mockGroupRepository.Object, configuration);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, amount));
        Assert.Equal("groupId", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeBudget_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var groupId = 1;
        var amount = -100000m;
        var group = new Group("Test Group");

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository
            .Setup(repo => repo.GetByIdAsync(groupId))
            .ReturnsAsync(group);

        var configuration = CreateConfiguration(4);

        var useCase = new IncreaseBudgetLimitUseCase(mockGroupRepository.Object, configuration);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(groupId, amount));
        Assert.Equal("amount", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectFiscalYear_FromConfiguration()
    {
        // Arrange
        var groupId = 1;
        var amount = 50000m;
        var group = new Group("Test Group");
        var fiscalYearStartMonth = 7;

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository
            .Setup(repo => repo.GetByIdAsync(groupId))
            .ReturnsAsync(group);

        var configuration = CreateConfiguration(fiscalYearStartMonth);

        var useCase = new IncreaseBudgetLimitUseCase(mockGroupRepository.Object, configuration);

        // Act
        await useCase.ExecuteAsync(groupId, amount);

        // Assert
        Assert.Single(group.BudgetTransactions);
        var transaction = group.BudgetTransactions.First();
        Assert.Equal(fiscalYearStartMonth, transaction.FiscalYear.StartMonth);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrectFiscalYear_FromNestedConfiguration()
    {
        // Arrange
        var groupId = 1;
        var amount = 50000m;
        var group = new Group("Test Group");
        var fiscalYearStartMonth = 7;

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository
            .Setup(repo => repo.GetByIdAsync(groupId))
            .ReturnsAsync(group);

        var configuration = CreateConfiguration(fiscalYearStartMonth);
        var useCase = new IncreaseBudgetLimitUseCase(mockGroupRepository.Object, configuration);

        // Act
        await useCase.ExecuteAsync(groupId, amount);

        // Assert
        Assert.Single(group.BudgetTransactions);
        var transaction = group.BudgetTransactions.First();
        Assert.Equal(fiscalYearStartMonth, transaction.FiscalYear.StartMonth);
    }

    private IConfiguration CreateConfiguration(int fiscalYearStartMonth)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "FiscalYearStartMonth:Month", fiscalYearStartMonth.ToString() }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }
}
