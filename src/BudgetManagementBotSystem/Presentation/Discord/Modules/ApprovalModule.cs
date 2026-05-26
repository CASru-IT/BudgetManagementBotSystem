using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ApprovalModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ApproveBudgetRequestUseCase _approveUseCase;
        private readonly RejectBudgetRequestUseCase _rejectUseCase;
        private readonly GetPendingRequestsUseCase _getPendingUseCase;
        private readonly RequestQueryUseCase _requestQueryUseCase;
        private readonly RevokeApprovalUseCase _revokeUseCase;

        public ApprovalModule(
            ApproveBudgetRequestUseCase approveUseCase,
            RejectBudgetRequestUseCase rejectUseCase,
            GetPendingRequestsUseCase getPendingUseCase,
            RequestQueryUseCase requestQueryUseCase,
            RevokeApprovalUseCase revokeUseCase)
        {
            _approveUseCase = approveUseCase;
            _rejectUseCase = rejectUseCase;
            _getPendingUseCase = getPendingUseCase;
            _requestQueryUseCase = requestQueryUseCase;
            _revokeUseCase = revokeUseCase;
        }

        [SlashCommand("pending-list", "未承認の申請一覧を表示する")]
        public async Task PendingList(int page = 1, int pageSize = 10)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var result = await _getPendingUseCase.ExecuteAsync(discordUserId, page, pageSize);

                if (result.Total == 0 || !result.Items.Any())
                {
                    await RespondAsync("未承認の申請は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = result.Items.Select(r =>
                    $"ID:{r.Id} 金額:{r.Amount:C} 日付:{r.RequestDate:yyyy-MM-dd} 説明:{(r.Description.Length>80? r.Description.Substring(0,80)+"...": r.Description)}");

                var header = $"未承認申請一覧 (ページ {result.Page}/{Math.Max(1, (int)Math.Ceiling(result.Total/(double)result.PageSize))}) 合計:{result.Total}";
                await RespondAsync($"{header}\n{string.Join("\n", lines)}");
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
                await RespondAsync($"申請 {requestId} を承認しました。");
            }
            catch (Exception ex)
            {
                await RespondAsync($"承認処理中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("reject", "指定した申請を却下する")]
        public async Task Reject(int requestId)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(discordUserId);
                var groupId = await _requestQueryUseCase.GetGroupIdByRequestIdAsync(requestId);

                await _rejectUseCase.ExecuteAsync(groupId, requestId, userId);
                await RespondAsync($"申請 {requestId} を却下しました。");
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
    }
}
