using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class RejectBudgetRequestUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInputs_UpdatesRequestStatusToRejected()
    {
        // Arrange
        const int groupId = 1;
        const int requestId = 1;
        const int changedByUserId = 2;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var group = new Group("Test Group");

        // リクエストをグループに追加
        var request = new BudgetRequest(
            userId: 1,
            amount: new Money(50_000m),
            fiscalYear: new FiscalYear(4),
            description: "備品購入"
        );
        // ID をリフレクションで設定
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);
        group.Requests.Add(request);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);

        var useCase = new RejectBudgetRequestUseCase(mockGroupRepository.Object, mockUserRepository.Object);

        // Act
        await useCase.ExecuteAsync(groupId, requestId, changedByUserId);

        // Assert
        Assert.Equal(RequestStatus.Rejected, request.StatusHistory.Last().ChangedStatus);
        mockGroupRepository.Verify(r => r.UpdateAsync(group), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGroupNotFound_ThrowsArgumentNullException()
    {
        // Arrange
        const int groupId = 999;
        const int requestId = 1;
        const int changedByUserId = 2;

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync((Group?)null);

        var mockUserRepository = new Mock<IUserRepository>();

        var useCase = new RejectBudgetRequestUseCase(mockGroupRepository.Object, mockUserRepository.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("groupId", ex.ParamName);
        mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApproverUserNotFound_ThrowsArgumentNullException()
    {
        // Arrange
        const int groupId = 1;
        const int requestId = 1;
        const int changedByUserId = 999;

        var group = new Group("Test Group");
        var request = new BudgetRequest(
            userId: 1,
            amount: new Money(50_000m),
            fiscalYear: new FiscalYear(4),
            description: "備品購入"
        );
        group.Requests.Add(request);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync((User?)null);

        var useCase = new RejectBudgetRequestUseCase(mockGroupRepository.Object, mockUserRepository.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("changedByUserId", ex.ParamName);
        mockGroupRepository.Verify(r => r.UpdateAsync(It.IsAny<Group>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestNotFound_ThrowsArgumentNullException()
    {
        // Arrange
        const int groupId = 1;
        const int requestId = 999;
        const int changedByUserId = 2;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var group = new Group("Test Group"); // リクエストなし

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);

        var useCase = new RejectBudgetRequestUseCase(mockGroupRepository.Object, mockUserRepository.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("requestId", ex.ParamName);
        mockGroupRepository.Verify(r => r.UpdateAsync(It.IsAny<Group>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRequests_UpdatesOnlyTargetRequest()
    {
        // Arrange
        const int groupId = 1;
        const int targetRequestId = 2;
        const int changedByUserId = 3;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var group = new Group("Test Group");

        // 複数のリクエストを追加
        var request1 = new BudgetRequest(
            userId: 1,
            amount: new Money(30_000m),
            fiscalYear: new FiscalYear(4),
            description: "PC購入"
        );
        var request2 = new BudgetRequest(
            userId: 2,
            amount: new Money(50_000m),
            fiscalYear: new FiscalYear(4),
            description: "モニター購入"
        );

        // ID をリフレクションで設定
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request1, 1);
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request2, 2);

        group.Requests.Add(request1);
        group.Requests.Add(request2);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);

        var useCase = new RejectBudgetRequestUseCase(mockGroupRepository.Object, mockUserRepository.Object);

        // Act
        await useCase.ExecuteAsync(groupId, targetRequestId, changedByUserId);

        // Assert
        Assert.Equal(RequestStatus.Rejected, request2.StatusHistory.Last().ChangedStatus);
        Assert.Equal(RequestStatus.Pending, request1.StatusHistory.Last().ChangedStatus); // 他のリクエストは変わらない
        mockGroupRepository.Verify(r => r.UpdateAsync(group), Times.Once);
    }
}
