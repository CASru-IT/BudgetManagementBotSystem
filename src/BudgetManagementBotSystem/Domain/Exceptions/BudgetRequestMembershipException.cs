namespace BudgetManagementBotSystem.Domain.Exceptions;

public sealed class BudgetRequestMembershipException : InvalidOperationException
{
    public IReadOnlyList<int> UserGroupIds { get; }
    public int RequestedGroupId { get; }
    public string RequestedGroupName { get; }

    public BudgetRequestMembershipException(IEnumerable<int> userGroupIds, int requestedGroupId, string requestedGroupName)
        : base("この予算申請のユーザーはグループのメンバーではありません。")
    {
        UserGroupIds = Array.AsReadOnly(userGroupIds.Distinct().OrderBy(id => id).ToArray());
        RequestedGroupId = requestedGroupId;
        RequestedGroupName = requestedGroupName;
    }
}
