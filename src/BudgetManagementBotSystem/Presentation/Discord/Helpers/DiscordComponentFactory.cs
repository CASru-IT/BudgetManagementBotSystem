using BudgetManagementBotSystem.Domain.Enums;
using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class DiscordComponentFactory
{
    public static MessageComponent BuildPagingComponents(string token, int currentPage, int totalPages)
    {
        return new ComponentBuilder()
            .WithButton("前へ", $"page:prev:{token}", ButtonStyle.Secondary, disabled: currentPage <= 1)
            .WithButton("更新", $"page:refresh:{token}", ButtonStyle.Primary)
            .WithButton("次へ", $"page:next:{token}", ButtonStyle.Secondary, disabled: currentPage >= totalPages)
            .Build();
    }

    public static MessageComponent? BuildRequestDetailComponents(int requestId, RequestStatus status)
    {
        if (status != RequestStatus.Pending)
        {
            return null;
        }

        return new ComponentBuilder()
            .WithButton("承認", $"request:approve:{requestId}", ButtonStyle.Success)
            .WithButton("却下", $"request:reject:{requestId}", ButtonStyle.Danger)
            .Build();
    }
}
