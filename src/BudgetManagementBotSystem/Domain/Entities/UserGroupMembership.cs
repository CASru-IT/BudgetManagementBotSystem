namespace BudgetManagementBotSystem.Domain.Entities;

public sealed class UserGroupMembership
{
    public int UserId { get; private set; }
    public int GroupId { get; private set; }

    internal UserGroupMembership(int groupId) => GroupId = groupId;
}
