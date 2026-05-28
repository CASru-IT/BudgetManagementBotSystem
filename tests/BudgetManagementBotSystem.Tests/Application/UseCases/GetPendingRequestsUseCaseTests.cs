using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class GetPendingRequestsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyPendingRequests()
    {
        const ulong discordUserId = 11111UL;
        const int pendingRequestId = 1;
        const int approvedRequestId = 2;

        var user = new User("Admin", discordUserId, AccountRole.Admin);
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester, new Money(10_000m), new FiscalYear(4), "Pending request", Array.Empty<string>());
        _ = group.CreateBudgetRequest(requester, new Money(20_000m), new FiscalYear(4), "Approved request", Array.Empty<string>());

        var pendingRequest = group.Requests.First(r => r.Description == "Pending request");
        var approvedRequest = group.Requests.First(r => r.Description == "Approved request");

        typeof(BudgetRequest).GetProperty("Id")!.SetValue(pendingRequest, pendingRequestId);
        typeof(BudgetRequest).GetProperty("Id")!.SetValue(approvedRequest, approvedRequestId);
        approvedRequest.UpdateStatus(RequestStatus.Approved);

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([group]);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);

        var useCase = new GetPendingRequestsUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object);

        var result = await useCase.ExecuteAsync(discordUserId);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(pendingRequestId, result.Items[0].Id);
        Assert.Equal("Pending request", result.Items[0].Description);
        Assert.DoesNotContain(result.Items, item => item.Id == approvedRequestId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAllPendingRequests_WhenPageSizeIsZero()
    {
        const ulong discordUserId = 11111UL;

        var user = new User("Admin", discordUserId, AccountRole.Admin);
        var requester = new User("Requester", 22222UL, AccountRole.Accountant);
        requester.ChangeGroupId(0);
        var group = new Group("Test Group");

        _ = group.CreateBudgetRequest(requester, new Money(10_000m), new FiscalYear(4), "Pending request 1", Array.Empty<string>());
        _ = group.CreateBudgetRequest(requester, new Money(20_000m), new FiscalYear(4), "Pending request 2", Array.Empty<string>());

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([group]);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(user);

        var useCase = new GetPendingRequestsUseCase(
            mockGroupRepository.Object,
            mockUserRepository.Object);

        var result = await useCase.ExecuteAsync(discordUserId, page: 2, pageSize: 0);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
    }
}