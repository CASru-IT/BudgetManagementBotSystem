using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class SubmitBudgetRequestUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInputAndWithinBudget_AddsPendingRequestAndUpdatesGroup()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 1;
        const decimal amount = 50_000m;
        const string description = "備品購入";

        var user = new User("Test User", 12345UL, AccountRole.Accountant);
        var group = new Group("Test Group");
        group.AddBudgetTransaction(new BudgetTransaction(true, 200_000m, new FiscalYear(4)));

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var configuration = CreateConfiguration(4);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act
        await useCase.ExecuteAsync(userId, groupId, amount, description);

        // Assert
        Assert.Single(group.Requests);
        var request = group.Requests.Single();
        Assert.Equal(amount, request.Amount.Value);
        Assert.Equal(description, request.Description);
        Assert.Equal(RequestStatus.Pending, request.StatusHistory.Last().ChangedStatus);

        mockGroupRepository.Verify(r => r.UpdateAsync(group), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBudgetIsInsufficient_RejectsRequestAndUpdatesGroup()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 1;
        const decimal amount = 10_000m;

        var user = new User("Test User", 12345UL, AccountRole.Accountant);
        var group = new Group("Test Group"); // 予算0

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var configuration = CreateConfiguration(4);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act
        await useCase.ExecuteAsync(userId, groupId, amount, "予算超過テスト");

        // Assert
        Assert.Single(group.Requests);
        var request = group.Requests.Single();
        Assert.Equal(RequestStatus.Rejected, request.StatusHistory.Last().ChangedStatus);

        mockGroupRepository.Verify(r => r.UpdateAsync(group), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsArgumentNullException()
    {
        // Arrange
        const int userId = 999;
        const int groupId = 1;

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        var mockGroupRepository = new Mock<IGroupRepository>();
        var configuration = CreateConfiguration(4);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(userId, groupId, 1_000m, "test"));

        Assert.Equal("userId", ex.ParamName);
        mockGroupRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGroupNotFound_ThrowsArgumentNullException()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 999;

        var user = new User("Test User", 12345UL, AccountRole.Accountant);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync((Group?)null);

        var configuration = CreateConfiguration(4);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(userId, groupId, 1_000m, "test"));

        Assert.Equal("groupId", ex.ParamName);
        mockGroupRepository.Verify(r => r.UpdateAsync(It.IsAny<Group>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAmountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 1;

        var user = new User("Test User", 12345UL, AccountRole.Accountant);
        var group = new Group("Test Group");

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var configuration = CreateConfiguration(4);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(userId, groupId, -1m, "test"));

        Assert.Equal("amount", ex.ParamName);
        mockGroupRepository.Verify(r => r.UpdateAsync(It.IsAny<Group>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFiscalYearStartMonth_FromConfiguration()
    {
        // Arrange
        const int userId = 1;
        const int groupId = 1;
        const int fiscalYearStartMonth = 7;

        var user = new User("Test User", 12345UL, AccountRole.Accountant);
        var group = new Group("Test Group");
        group.AddBudgetTransaction(new BudgetTransaction(true, 100_000m, new FiscalYear(fiscalYearStartMonth)));

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var configuration = CreateConfiguration(fiscalYearStartMonth);
        var useCase = new SubmitBudgetRequestUseCase(
            mockUserRepository.Object,
            mockGroupRepository.Object,
            configuration);

        // Act
        await useCase.ExecuteAsync(userId, groupId, 10_000m, "fiscal year test");

        // Assert
        Assert.Single(group.Requests);
        var request = group.Requests.Single();
        Assert.Equal(fiscalYearStartMonth, request.FiscalYear.StartMonth);
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
}
