using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord.Interactions;

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

        [SlashCommand("pending-list", "未承認の申請一覧を表示します")]
        public async Task PendingList(int page = 1, [Summary("page-size")] int? pageSize = null)
        {
            try
            {
                var result = await _getPendingUseCase.ExecuteAsync(Context.User.Id, Math.Max(1, page), pageSize ?? 0);
                await RespondAsync(embed: DiscordEmbedFactory.BuildPendingRequestsEmbed(result), ephemeral: result.Total == 0);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("未承認申請を確認する権限がありません。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("未承認申請を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("approve", "指定した申請を承認します")]
        public async Task Approve(int requestId)
        {
            try
            {
                var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(Context.User.Id);
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

                await RespondAsync(embed: DiscordEmbedFactory.BuildApprovalResultEmbed(requestId, notificationSent));
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請を承認する権限がありません。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", requestId.ToString()), ephemeral: true);
            }
            catch (InvalidOperationException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請状態を確認してください。承認できるのは承認待ちの申請です。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("承認処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("reject", "指定した申請を却下します")]
        public async Task Reject(int requestId, string reason)
        {
            try
            {
                var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(Context.User.Id);
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

                await RespondAsync(embed: DiscordEmbedFactory.BuildRejectionResultEmbed(requestId, notificationSent, reason));
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請を却下する権限がありません。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", requestId.ToString()), ephemeral: true);
            }
            catch (InvalidOperationException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請状態を確認してください。却下できるのは承認待ちの申請です。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("却下処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("revoke-approval", "承認済み申請の承認を取り消します")]
        public async Task RevokeApproval([Summary("request-id")] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var parsedRequestId))
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
                    return;
                }

                await _revokeUseCase.ExecuteAsync(parsedRequestId, Context.User.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("承認を取り消しました", $"申請 `#{parsedRequestId}` の承認取消が完了しました。"), ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("承認を取り消す権限がありません。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", requestId), ephemeral: true);
            }
            catch (InvalidOperationException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請状態を確認してください。承認取消できるのは承認済みの申請です。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("承認取消を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
