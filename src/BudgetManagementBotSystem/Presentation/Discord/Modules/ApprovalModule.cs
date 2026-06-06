using Discord;
using Discord.Interactions;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ApprovalModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ApproveBudgetRequestUseCase _approveUseCase;
        private readonly RejectBudgetRequestUseCase _rejectUseCase;
        private readonly GetPendingRequestsUseCase _getPendingUseCase;
        private readonly RequestQueryUseCase _requestQueryUseCase;
        private readonly NotifyApprovedRequestUseCase _notifyApprovedRequestUseCase;
        private readonly NotifyRejectedRequestUseCase _notifyRejectedRequestUseCase;
        private readonly RevokeApprovalUseCase _revokeUseCase;
        private readonly DiscordBotService _discordBotService;

        public ApprovalModule(
            ApproveBudgetRequestUseCase approveUseCase,
            RejectBudgetRequestUseCase rejectUseCase,
            GetPendingRequestsUseCase getPendingUseCase,
            RequestQueryUseCase requestQueryUseCase,
            NotifyApprovedRequestUseCase notifyApprovedRequestUseCase,
            NotifyRejectedRequestUseCase notifyRejectedRequestUseCase,
            RevokeApprovalUseCase revokeUseCase,
            DiscordBotService discordBotService)
        {
            _approveUseCase = approveUseCase;
            _rejectUseCase = rejectUseCase;
            _getPendingUseCase = getPendingUseCase;
            _requestQueryUseCase = requestQueryUseCase;
            _notifyApprovedRequestUseCase = notifyApprovedRequestUseCase;
            _notifyRejectedRequestUseCase = notifyRejectedRequestUseCase;
            _revokeUseCase = revokeUseCase;
            _discordBotService = discordBotService;
        }

        [SlashCommand("pending-list", "未承認の申請一覧を表示する")]
        public async Task PendingList(int page = 1, [Summary("page-size")] int? pageSize = null)
        {
            try
            {
                var discordUserId = Context.User.Id;
            var result = await _getPendingUseCase.ExecuteAsync(discordUserId, page, pageSize ?? 0);

                if (result.Total == 0 || !result.Items.Any())
                {
                    var emptyEmbed = new EmbedBuilder()
                        .WithTitle("未承認申請一覧")
                        .WithColor(Color.Blue)
                        .WithDescription("未承認の申請は見つかりませんでした。")
                        .WithFooter("承認/却下は /approve /reject コマンドを使ってください")
                        .Build();

                    await RespondAsync(embed: emptyEmbed, ephemeral: true);
                    return;
                }

                var embed = DiscordEmbedFactory.BuildPendingRequestsEmbed(result);
                await RespondAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await RespondAsync($"一覧取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("approve", "指定した申請を承認する")]
        public async Task Approve(int requestId)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(discordUserId);
                var groupId = await _requestQueryUseCase.GetGroupIdByRequestIdAsync(requestId);

                await _approveUseCase.ExecuteAsync(groupId, requestId, userId);

                var notification = await _notifyApprovedRequestUseCase.ExecuteAsync(requestId, userId);
                var notificationSent = false;

                if (notification != null)
                {
                    notificationSent = await _discordBotService.SendDirectMessageAsync(
                        notification.RequesterDiscordUserId,
                        DiscordEmbedFactory.BuildApprovedRequestDmEmbed(notification));
                }

                var resultEmbed = DiscordEmbedFactory.BuildApprovalResultEmbed(requestId, notificationSent);
                await RespondAsync(embed: resultEmbed);
            }
            catch (Exception ex)
            {
                await RespondAsync($"承認処理中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("reject", "指定した申請を却下する")]
        public async Task Reject(int requestId, [Summary("reason")] string reason = "")
        {
            try
            {
                var discordUserId = Context.User.Id;
                var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(discordUserId);
                var groupId = await _requestQueryUseCase.GetGroupIdByRequestIdAsync(requestId);

                await _rejectUseCase.ExecuteAsync(groupId, requestId, userId);

                var notification = await _notifyRejectedRequestUseCase.ExecuteAsync(requestId, userId, reason);
                var notificationSent = false;

                if (notification != null)
                {
                    notificationSent = await _discordBotService.SendDirectMessageAsync(
                        notification.RequesterDiscordUserId,
                        DiscordEmbedFactory.BuildRejectedRequestDmEmbed(notification));
                }

                var resultEmbed = DiscordEmbedFactory.BuildRejectionResultEmbed(requestId, notificationSent);
                await RespondAsync(embed: resultEmbed);
            }
            catch (Exception ex)
            {
                await RespondAsync($"却下処理中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("revoke-approval", "承認済み申請の承認を取り消す")]
        public async Task RevokeApproval([Summary("request-id")] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var reqId))
                {
                    await RespondAsync($"申請IDは数値で指定してください: {requestId}", ephemeral: true);
                    return;
                }
                var discordUserId = Context.User.Id;
                await _revokeUseCase.ExecuteAsync(reqId, discordUserId);

                await RespondAsync($"申請 {reqId} の承認を取り消しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"承認取消中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        private static string BuildApprovedRequestMessage(ApprovedRequestNotificationDto notification)
        {
            return $"申請 {notification.RequestId} が承認されました。";
        }
    }
}
