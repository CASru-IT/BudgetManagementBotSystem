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

        public static Embed BuildApprovalResultEmbed(int requestId, bool notificationSent)
        {
            var embed = new EmbedBuilder()
                .WithTitle("申請の承認が完了しました")
                .WithColor(Color.Green)
                .WithDescription($"申請 ID {requestId} の承認が正常に完了しました。")
                .AddField("申請ID", requestId, true)
                .AddField("通知", notificationSent ? "申請者へDMを送信しました" : "DM送信に失敗しました", true)
                .WithFooter("申請者へ承認通知が送信されます。")
                .Build();

            return embed;
        }

        public static Embed BuildApprovedRequestDmEmbed(ApprovedRequestNotificationDto notification)
        {
            var description = notification.Description.Length > 80
                ? notification.Description.Substring(0, 80) + "..."
                : notification.Description;

            var embed = new EmbedBuilder()
                .WithTitle("申請が承認されました")
                .WithColor(Color.Green)
                .WithDescription("あなたの申請は承認されました。下記の内容を確認してください。")
                .AddField("申請ID", notification.RequestId, true)
                .AddField("班名", notification.GroupName, true)
                .AddField("金額", notification.Amount.ToString("C"), true)
                .AddField("説明", description)
                .AddField("承認者", notification.ApproverName, true)
                .AddField("承認者 Discord", notification.ApproverDiscordUserId.ToString(), true)
                .WithFooter("会計担当と受け取り日時を調整してください。")
                .Build();

            return embed;
        }
    }
}
