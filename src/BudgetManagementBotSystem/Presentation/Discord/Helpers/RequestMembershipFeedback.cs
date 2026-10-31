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
        var registeredGroup = "未設定";
        if (error.UserGroupId is int groupId)
        {
            registeredGroup = $"班ID: {groupId}";
            try
            {
                var group = await groups.GetByIdAsync(groupId);
                if (group != null)
                {
                    registeredGroup = $"{group.Name}（班ID: {groupId}）";
                }
            }
            catch (Exception ex)
            {
                // A failed name lookup must not hide the already-known rejection reason.
                logger.LogWarning(ex, "Failed to resolve membership group name. GroupId: {GroupId}", groupId);
            }
        }

        var reason = error.UserGroupId.HasValue
            ? "登録されている所属班と申請先の班が異なるため、申請できません。"
            : "所属班が登録されていないため、申請できません。";
        var action = error.UserGroupId.HasValue
            ? "所属班の予算へ申請する場合は、`/create-request` で所属班を選び直してください。\n"
            : "";

        return DiscordEmbedFactory.BuildWarningEmbed("所属班を確認してください",
            $"{reason}\n\n" +
            $"登録されている所属班: {registeredGroup}\n" +
            $"申請先: {error.RequestedGroupName}（班ID: {error.RequestedGroupId}）\n\n" +
            "予算申請は、登録されている所属班に対してのみ行えます。\n" + action +
            "所属が未設定、または登録内容が実際と異なる場合は、管理者に所属の確認・修正（`/assign-group`）を依頼してください。\n\n" +
            "申請は作成されていません。");
    }
}
