using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ApprovalModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly GetPendingRequestsUseCase _getPendingUseCase;
        private readonly RevokeApprovalUseCase _revokeUseCase;
        private readonly RequestWorkflowInteractionService _requestWorkflowInteractionService;
        private readonly PagingSessionStore _pagingSessionStore;
        private readonly ILogger<ApprovalModule> _logger;

        public ApprovalModule(
            GetPendingRequestsUseCase getPendingUseCase,
            RevokeApprovalUseCase revokeUseCase,
            RequestWorkflowInteractionService requestWorkflowInteractionService,
            PagingSessionStore pagingSessionStore,
            ILogger<ApprovalModule> logger)
        {
            _getPendingUseCase = getPendingUseCase;
            _revokeUseCase = revokeUseCase;
            _requestWorkflowInteractionService = requestWorkflowInteractionService;
            _pagingSessionStore = pagingSessionStore;
            _logger = logger;
        }

        [SlashCommand("pending-list", "未承認の申請一覧を表示します")]
        public async Task PendingList(int page = 1, [Summary("page-size")] int? pageSize = null)
        {
            try
            {
                var requestedPageSize = pageSize ?? 10;
                var result = await _getPendingUseCase.ExecuteAsync(Context.User.Id, Math.Max(1, page), requestedPageSize);
                var totalPages = CalculateTotalPages(result.Total, result.PageSize);
                var components = totalPages > 1
                    ? DiscordComponentFactory.BuildPagingComponents(
                        _pagingSessionStore.Create(new PagingSession
                        {
                            OwnerDiscordUserId = Context.User.Id,
                            Target = PagingTarget.PendingList,
                            Page = result.Page,
                            PageSize = result.PageSize
                        }),
                        result.Page,
                        totalPages)
                    : null;

                await RespondAsync(
                    embed: DiscordEmbedFactory.BuildPendingRequestsEmbed(result),
                    components: components,
                    ephemeral: result.Total == 0);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("未承認申請を確認する権限がありません。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending requests. DiscordUserId: {DiscordUserId}, Page: {Page}, PageSize: {PageSize}", Context.User.Id, page, pageSize);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("未承認申請を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("approve", "指定した申請を承認します")]
        public async Task Approve([Summary("request-id"), Autocomplete(typeof(RequestAutocompleteHandler))] int requestId)
        {
            try
            {
                var notificationSent = await _requestWorkflowInteractionService.ApproveAsync(requestId, Context.User.Id);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to approve request. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, requestId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("承認処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("reject", "指定した申請を却下します")]
        public async Task Reject([Summary("request-id"), Autocomplete(typeof(RequestAutocompleteHandler))] int requestId, string reason)
        {
            try
            {
                var notificationSent = await _requestWorkflowInteractionService.RejectAsync(requestId, Context.User.Id, reason);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reject request. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, requestId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("却下処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("revoke-approval", "承認済み申請の承認を取り消します")]
        public async Task RevokeApproval([Summary("request-id"), Autocomplete(typeof(RequestAutocompleteHandler))] string requestId)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke approval. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, requestId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("承認取消を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        private static int CalculateTotalPages(int total, int pageSize)
        {
            return pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        }
    }
}
