using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class ApproveBudgetRequestUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInputs_UpdatesRequestStatusToApproved()
    {
        const int groupId = 1;
        const int requestId = 1;
        const int changedByUserId = 2;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入");
        var request = group.Requests.Single();
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new ApproveBudgetRequestUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        await useCase.ExecuteAsync(groupId, requestId, changedByUserId);

        Assert.Equal(RequestStatus.Approved, request.StatusHistory.Last().ChangedStatus);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGroupNotFound_ThrowsArgumentNullException()
    {
        const int groupId = 999;
        const int requestId = 1;
        const int changedByUserId = 2;

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync((Group?)null);

        var mockUserRepository = new Mock<IUserRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new ApproveBudgetRequestUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("groupId", ex.ParamName);
        mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApproverUserNotFound_ThrowsArgumentNullException()
    {
        const int groupId = 1;
        const int requestId = 1;
        const int changedByUserId = 999;

        var group = new Group("Test Group");
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入");

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync((User?)null);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new ApproveBudgetRequestUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("changedByUserId", ex.ParamName);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestNotFound_ThrowsArgumentNullException()
    {
        const int groupId = 1;
        const int requestId = 999;
        const int changedByUserId = 2;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var group = new Group("Test Group");

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new ApproveBudgetRequestUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(groupId, requestId, changedByUserId)
        );

        Assert.Equal("requestId", ex.ParamName);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRequests_UpdatesOnlyTargetRequest()
    {
        const int groupId = 1;
        const int targetRequestId = 2;
        const int changedByUserId = 3;

        var changedByUser = new User("Approver", 11111UL, AccountRole.Admin);
        var requester1 = new User("Requester1", 22222UL, AccountRole.Accountant);
        var requester2 = new User("Requester2", 33333UL, AccountRole.Accountant);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester1, new Money(30_000m), new FiscalYear(4), "PC購入");
        _ = group.CreateBudgetRequest(requester2, new Money(50_000m), new FiscalYear(4), "モニター購入");
        var request1 = group.Requests.First(r => r.Description == "PC購入");
        var request2 = group.Requests.First(r => r.Description == "モニター購入");

        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request1, 1);
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request2, 2);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(changedByUserId)).ReturnsAsync(changedByUser);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new ApproveBudgetRequestUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        await useCase.ExecuteAsync(groupId, targetRequestId, changedByUserId);

        Assert.Equal(RequestStatus.Approved, request2.StatusHistory.Last().ChangedStatus);
        Assert.Equal(RequestStatus.Pending, request1.StatusHistory.Last().ChangedStatus);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
