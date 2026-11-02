using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.Repository;
using Discord;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class RequestMembershipFeedback
{
    public static async Task<Embed> BuildAsync(BudgetRequestMembershipException error,
        IGroupRepository groups, ILogger logger)
    {
        var registeredGroups = new List<string>();
        foreach (var groupId in error.UserGroupIds.Take(20))
        {
            var registeredGroup = $"班ID: {groupId}";
            try
            {
                var group = await groups.GetByIdAsync(groupId);
                if (group != null)
                {
                    registeredGroup = $"{LimitName(group.Name)}（班ID: {groupId}）";
                }
            }
            catch (Exception ex)
            {
                // A failed name lookup must not hide the already-known rejection reason.
                logger.LogWarning(ex, "Failed to resolve membership group name. GroupId: {GroupId}", groupId);
            }
            registeredGroups.Add(registeredGroup);
        }
        if (error.UserGroupIds.Count > 20) registeredGroups.Add($"ほか {error.UserGroupIds.Count - 20} 班");
        var registeredGroupLabel = registeredGroups.Count == 0 ? "未設定" : string.Join("、", registeredGroups);

        var reason = error.UserGroupIds.Count > 0
            ? "申請先の班が登録されている所属班に含まれていないため、申請できません。"
            : "所属班が登録されていないため、申請できません。";
        var action = error.UserGroupIds.Count > 0
            ? "所属班の予算へ申請する場合は、`/create-request` で所属班を選び直してください。\n"
            : "";

        return new EmbedBuilder().WithTitle("所属班を確認してください").WithColor(Color.Gold).WithDescription(
            $"{reason}\n\n" +
            $"登録されている所属班: {registeredGroupLabel}\n" +
            $"申請先: {LimitName(error.RequestedGroupName)}（班ID: {error.RequestedGroupId}）\n\n" +
            "予算申請は、登録されている所属班に対してのみ行えます。\n" + action +
            "この班にも所属している場合は、管理者に `/assign-group` で所属の追加を依頼してください。既存の所属は残ります。\n\n" +
            "申請は作成されていません。").Build();
    }

    private static string LimitName(string name) => name.Length <= 100 ? name : name[..99] + "…";
}
