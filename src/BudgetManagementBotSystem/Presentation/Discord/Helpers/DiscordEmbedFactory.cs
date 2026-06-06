using System;
using System.Linq;
using Discord;
using BudgetManagementBotSystem.Application.DTOs;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers
{
    public static class DiscordEmbedFactory
    {
        public static Embed BuildPendingRequestsEmbed(PagedResult<PendingRequestDto> result)
        {
            var totalPages = result.PageSize <= 0
                ? 1
                : Math.Max(1, (int)Math.Ceiling(result.Total / (double)result.PageSize));

            var embed = new EmbedBuilder()
                .WithTitle("未承認申請一覧")
                .WithColor(Color.Blue)
                .WithFooter("承認/却下は /approve /reject コマンドを使ってください")
                .AddField("ページ", $"{result.Page}/{totalPages}", true)
                .AddField("合計申請数", result.Total.ToString(), true);

            if (!result.Items.Any())
            {
                embed.WithDescription("現在、未承認の申請はありません。");
                return embed.Build();
            }

            foreach (var request in result.Items)
            {
                var description = request.Description.Length > 80
                    ? request.Description.Substring(0, 80) + "..."
                    : request.Description;

                embed.AddField(
                    $"ID {request.Id} ・ {request.GroupName} ・ {request.Amount:C}",
                    $"`{request.RequestDate:yyyy-MM-dd}`\n{description}");
            }

            return embed.Build();
        }
    }
}
