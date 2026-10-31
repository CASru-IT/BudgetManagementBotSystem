namespace BudgetManagementBotSystem.Application.Exceptions;

public sealed class GroupSelectionRequiredException : InvalidOperationException
{
    public GroupSelectionRequiredException(IReadOnlyList<int> groupIds)
        : base(groupIds.Count == 0
            ? "所属班が未設定です。group-id で表示する班を指定してください。所属の登録は管理者に依頼してください。"
            : "複数の班に所属しています。group-id で表示する班を指定してください。") { }
}
