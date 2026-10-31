using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class MultipleGroupMembershipTests
{
    [Fact]
    public async Task AssigningSecondGroup_PreservesPermissionToRequestFromBothGroups()
    {
        var first = new Group("ボードゲーム");
        var second = new Group("執行部");
        typeof(Group).GetProperty("Id")!.SetValue(first, 11);
        typeof(Group).GetProperty("Id")!.SetValue(second, 10);
        var user = new User("兼任者", 123, AccountRole.GroupLeader, 11);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByDiscordUserIdAsync(123)).ReturnsAsync(user);
        var groups = new Mock<IGroupRepository>();
        groups.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(second);
        var useCase = new UserCommandUseCase(users.Object, groups.Object, Mock.Of<IUnitOfWork>());

        await useCase.AssignGroupByDiscordIdAsync(123, 10);

        first.EnsureRequestMembership(user);
        second.EnsureRequestMembership(user);
    }
}
