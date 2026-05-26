using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class RevokeApprovalUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithAccountant_RevokesApprovedRequest()
    {
        const int requestId = 1;
        const ulong discordUserId = 11111UL;

        var actingUser = new User("Accountant", discordUserId, AccountRole.Accountant);
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入", Array.Empty<string>());
        var request = group.Requests.Single();
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);
        request.UpdateStatus(RequestStatus.Approved, actingUser);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Group> { group });

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(actingUser);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new RevokeApprovalUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        await useCase.ExecuteAsync(requestId, discordUserId);

        Assert.Equal(RequestStatus.ApprovalCancelled, request.StatusHistory.Last().ChangedStatus);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActorIsNotAccountant_ThrowsUnauthorizedAccessException()
    {
        const int requestId = 1;
        const ulong discordUserId = 11111UL;

        var actingUser = new User("Admin", discordUserId, AccountRole.Admin);
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester, new Money(50_000m), new FiscalYear(4), "備品購入", Array.Empty<string>());
        var request = group.Requests.Single();
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(request, requestId);
        request.UpdateStatus(RequestStatus.Approved, actingUser);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Group> { group });

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(actingUser);
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new RevokeApprovalUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object,
            mockUnitOfWork.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => useCase.ExecuteAsync(requestId, discordUserId));

        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}