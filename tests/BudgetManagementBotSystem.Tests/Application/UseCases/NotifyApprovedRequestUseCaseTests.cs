using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class NotifyApprovedRequestUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInputs_ReturnsNotificationData()
    {
        const int requestId = 1;
        const int approverUserId = 2;

        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        var approver = new User("Approver", 11111UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        approver.ChangeGroupId(0);
        typeof(User).GetProperty("Id")!.SetValue(approver, approverUserId);

        var group = new Group("Test Group");
        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入", Array.Empty<string>());
        var request = group.Requests.Single();
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Group> { group });

        var mockFileStorage = new Mock<IFileStorage>();
        var requestDetailUseCase = new RequestDetailUseCase(mockGroupRepository.Object, mockFileStorage.Object);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(approverUserId)).ReturnsAsync(approver);
        mockUserRepository.Setup(r => r.GetByIdAsync(requester.Id)).ReturnsAsync(requester);

        var useCase = new NotifyApprovedRequestUseCase(
            requestDetailUseCase,
            mockUserRepository.Object);

        var result = await useCase.ExecuteAsync(requestId, approverUserId);

        Assert.NotNull(result);
        Assert.IsType<ApprovedRequestNotificationDto>(result);
        Assert.Equal(requester.DiscordUserId, result!.RequesterDiscordUserId);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal("Test Group", result.GroupName);
        Assert.Equal(50_000m, result.Amount);
        Assert.Equal("備品購入", result.Description);
        Assert.Equal("Approver", result.ApproverName);
        Assert.Equal(approver.DiscordUserId, result.ApproverDiscordUserId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterNotFound_ReturnsNull()
    {
        const int requestId = 1;
        const int approverUserId = 2;

        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        var approver = new User("Approver", 11111UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        approver.ChangeGroupId(0);
        typeof(User).GetProperty("Id")!.SetValue(approver, approverUserId);

        var group = new Group("Test Group");
        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入", Array.Empty<string>());
        var request = group.Requests.Single();
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Group> { group });

        var mockFileStorage = new Mock<IFileStorage>();
        var requestDetailUseCase = new RequestDetailUseCase(mockGroupRepository.Object, mockFileStorage.Object);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(approverUserId)).ReturnsAsync(approver);
        mockUserRepository.Setup(r => r.GetByIdAsync(requester.Id)).ReturnsAsync((User?)null);

        var useCase = new NotifyApprovedRequestUseCase(
            requestDetailUseCase,
            mockUserRepository.Object);

        var result = await useCase.ExecuteAsync(requestId, approverUserId);

        Assert.Null(result);
    }
}