using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;

namespace BudgetManagementBotSystem.Tests.Domain.Entities;

public class UserMembershipTests
{
    [Fact]
    public void AddRemoveMemberships_IsIdempotentAndPreservesOtherGroups()
    {
        var user = new User("兼任", 1, AccountRole.GroupLeader, 11);
        user.AddGroup(10);
        user.AddGroup(10);
        Assert.Equal(new[] { 10, 11 }, user.GroupIds);
        Assert.Equal(2, user.Memberships.Count);
        user.RemoveGroup(11);
        user.RemoveGroup(11);
        Assert.Equal(new[] { 10 }, user.GroupIds);
        user.RemoveGroup(10);
        Assert.Empty(user.GroupIds);
        Assert.False(user.IsMemberOf(10));
    }

    [Theory]
    [InlineData(AccountRole.Admin)]
    [InlineData(AccountRole.Accountant)]
    [InlineData(AccountRole.GroupLeader)]
    [InlineData(AccountRole.President)]
    public void Requests_AllowEachMembershipButRejectUnregisteredGroup(AccountRole role)
    {
        var user = new User("兼任", 1, role, 10);
        user.AddGroup(11);
        foreach (var id in new[] { 10, 11 }) Group(id).EnsureRequestMembership(user);
        var exception = Assert.Throws<BudgetRequestMembershipException>(() => Group(12).EnsureRequestMembership(user));
        user.RemoveGroup(10);
        Assert.Equal(new[] { 10, 11 }, exception.UserGroupIds);
        Assert.Throws<BudgetRequestMembershipException>(() => Group(10).EnsureRequestMembership(user));
        Group(11).EnsureRequestMembership(user);
    }

    private static Group Group(int id)
    {
        var group = new Group("班" + id);
        typeof(Group).GetProperty("Id")!.SetValue(group, id);
        return group;
    }
}
