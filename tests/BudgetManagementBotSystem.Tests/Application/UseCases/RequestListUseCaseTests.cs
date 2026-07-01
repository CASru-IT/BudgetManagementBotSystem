using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class RequestListUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_GroupLeaderReturnsOnlyOwnGroupRequests()
    {
        const ulong discordUserId = 11111UL;
        var (groupA, groupB, leader) = CreateGroupsWithRequests(discordUserId, AccountRole.GroupLeader);

        var useCase = CreateUseCase(discordUserId, leader, groupA, groupB);

        var result = await useCase.ExecuteAsync(discordUserId, status: null, page: 1, pageSize: 10);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(groupA.Id, result.Items[0].GroupId);
    }

    [Fact]
    public async Task ExecuteAsync_GroupLeaderCannotViewOtherGroupWhenGroupIdIsSpecified()
    {
        const ulong discordUserId = 11111UL;
        var (groupA, groupB, leader) = CreateGroupsWithRequests(discordUserId, AccountRole.GroupLeader);

        var useCase = CreateUseCase(discordUserId, leader, groupA, groupB);

        var result = await useCase.ExecuteAsync(discordUserId, status: null, page: 1, pageSize: 10, groupId: groupB.Id);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(groupA.Id, result.Items[0].GroupId);
    }

    [Fact]
    public async Task ExecuteAsync_AdminReturnsAllGroupsWhenGroupIdIsNotSpecified()
    {
        const ulong discordUserId = 11111UL;
        var (groupA, groupB, admin) = CreateGroupsWithRequests(discordUserId, AccountRole.Admin);

        var useCase = CreateUseCase(discordUserId, admin, groupA, groupB);

        var result = await useCase.ExecuteAsync(discordUserId, status: null, page: 1, pageSize: 10);

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, item => item.GroupId == groupA.Id);
        Assert.Contains(result.Items, item => item.GroupId == groupB.Id);
    }

    private static (Group GroupA, Group GroupB, User Caller) CreateGroupsWithRequests(ulong discordUserId, AccountRole callerRole)
    {
        var groupA = new Group("Group A");
        var groupB = new Group("Group B");
        SetId(groupA, 1);
        SetId(groupB, 2);

        var caller = new User("Caller", discordUserId, callerRole, groupA.Id);
        SetId(caller, 10);

        var requesterA = new User("Requester A", 22222UL, AccountRole.GroupLeader, groupA.Id);
        var requesterB = new User("Requester B", 33333UL, AccountRole.GroupLeader, groupB.Id);
        SetId(requesterA, 20);
        SetId(requesterB, 30);

        var requestA = groupA.CreateBudgetRequest(requesterA, new Money(10_000m), new FiscalYear(2026), "Group A request", Array.Empty<string>());
        var requestB = groupB.CreateBudgetRequest(requesterB, new Money(20_000m), new FiscalYear(2026), "Group B request", Array.Empty<string>());
        SetId(requestA, 101);
        SetId(requestB, 102);

        return (groupA, groupB, caller);
    }

    private static RequestListUseCase CreateUseCase(ulong discordUserId, User caller, params Group[] groups)
    {
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(groups.ToList());

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(discordUserId)).ReturnsAsync(caller);

        return new RequestListUseCase(mockGroupRepository.Object, mockUserRepository.Object);
    }

    private static void SetId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
    }
}
