namespace BudgetManagementBotSystem.Domain.Exceptions;

public sealed class BudgetRequestMembershipException : InvalidOperationException
{
    public int? UserGroupId { get; }
    public int RequestedGroupId { get; }
    public string RequestedGroupName { get; }

    public BudgetRequestMembershipException(int? userGroupId, int requestedGroupId, string requestedGroupName)
        : base("この予算申請のユーザーはグループのメンバーではありません。")
    {
        UserGroupId = userGroupId;
        RequestedGroupId = requestedGroupId;
        RequestedGroupName = requestedGroupName;
    }
}
