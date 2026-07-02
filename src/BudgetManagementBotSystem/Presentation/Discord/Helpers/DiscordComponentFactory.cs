using BudgetManagementBotSystem.Domain.Enums;
using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class DiscordComponentFactory
{
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
