using Discord.Interactions;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SubmitBudgetRequestUseCase _submitBudgetRequestUseCase;
        private readonly CancelBudgetRequestUseCase _cancelBudgetRequestUseCase;
        private readonly UserCancelBudgetRequestUseCase _userCancelRequestUseCase;
        private readonly IUserRepository _userRepository;
        private readonly InfraStructure.Discord.DiscordBotService _discordBotService;
        private readonly RequestListUseCase _requestListUseCase;
        private readonly RequestDetailUseCase _requestDetailUseCase;
        private readonly RequestQueryUseCase _requestQueryUseCase;

        public RequestModule(SubmitBudgetRequestUseCase submitBudgetRequestUseCase, CancelBudgetRequestUseCase cancelBudgetRequestUseCase, UserCancelBudgetRequestUseCase userCancelRequestUseCase, IUserRepository userRepository, BudgetManagementBotSystem.InfraStructure.Discord.DiscordBotService discordBotService, RequestListUseCase requestListUseCase, RequestDetailUseCase requestDetailUseCase, RequestQueryUseCase requestQueryUseCase)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _cancelBudgetRequestUseCase = cancelBudgetRequestUseCase;
            _userCancelRequestUseCase = userCancelRequestUseCase;
            _userRepository = userRepository;
            _discordBotService = discordBotService;
            _requestListUseCase = requestListUseCase;
            _requestDetailUseCase = requestDetailUseCase;
            _requestQueryUseCase = requestQueryUseCase;
        }

        [SlashCommand("create-request", "予算使用申請を作成する")]
        public async Task CreateRequest(
            [Summary("group-id")] int groupId,
            [Summary("amount")] double amount,
            [Summary("description")] string description,
            [Summary("attach-count")]
            int attachCount = 1)
        {
            try
            {
                await DeferAsync(ephemeral: true);

                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await FollowupAsync("エラー: Discord ユーザーがシステムに登録されていません。管理者に登録を依頼してください。", ephemeral: true);
                    return;
                }

                decimal amountDec = Convert.ToDecimal(amount);

                var evidenceFiles = new List<UploadedEvidenceDto>();
                if (attachCount < 1)
                {
                    await FollowupAsync("添付ファイル数は1以上で指定してください。", ephemeral: true);
                    return;
                }

                await FollowupAsync($"証跡ファイルをこのチャンネルに {attachCount} 件添付してください。30秒以内にアップロードしてください。", ephemeral: true);
                var uploaded = await _discordBotService.WaitForAttachmentUploadAsync(Context.User.Id, TimeSpan.FromSeconds(30), attachCount, Context.Channel);
                // 指定数の証跡を受け取れなかった場合は申請を中止する
                if (uploaded == null || !uploaded.Any() || uploaded.Count < attachCount)
                {
                    await FollowupAsync("証跡ファイルの受け取りに失敗しました。申請を中止します。", ephemeral: true);
                    return;
                }

                evidenceFiles.AddRange(uploaded);
                await FollowupAsync("証跡ファイルを受け取りました。保存を開始します。", ephemeral: true);

                var savedEvidenceCount = await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, evidenceFiles);

                await FollowupAsync($"申請を作成しました: 班 {groupId} 金額 {amountDec:C}", ephemeral: true);
                if (savedEvidenceCount > 0)
                {
                    await FollowupAsync($"証跡ファイルの保存に成功しました: {savedEvidenceCount}件", ephemeral: true);
                }
            }
            catch (ArgumentNullException ex)
            {
                await FollowupAsync($"入力エラー: {ex.ParamName} - {ex.Message}", ephemeral: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                await FollowupAsync($"入力エラー: {ex.Message}", ephemeral: true);
            }
            catch (BudgetLimitExceededException ex)
            {
                await FollowupAsync(ex.Message, ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"予期せぬエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("list-requests", "自分の班または役員会の申請一覧を表示する")]
        public async Task ListRequests(
            [Summary("status")] string? status = null,
            [Summary("page")] int page = 1,
            [Summary("page-size")] int pageSize = 10,
            [Summary("group-id")] int? groupId = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                pageSize = Math.Min(pageSize, 50);

                var discordUserId = Context.User.Id;
                var result = await _requestListUseCase.ExecuteAsync(discordUserId, status, page, pageSize, groupId);

                if (result.Total == 0 || !result.Items.Any())
                {
                    await RespondAsync("該当する申請は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = result.Items.Select(r => $"ID:{r.Id} 金額:{r.Amount:C} 日付:{r.RequestDate:yyyy-MM-dd} 説明:{(r.Description.Length>80? r.Description.Substring(0,80)+"...": r.Description)}");
                var header = $"申請一覧 (ページ {result.Page}/{Math.Max(1, (int)Math.Ceiling(result.Total/(double)result.PageSize))}) 合計:{result.Total}";
                await RespondAsync($"{header}\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"申請一覧の取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("request-detail", "指定した申請の詳細を表示する")]
        public async Task RequestDetail([Summary("request-id")] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var reqId))
                {
                    await RespondAsync($"申請IDは数値で指定してください: {requestId}", ephemeral: true);
                    return;
                }

                var (req, gid) = await _requestDetailUseCase.GetByIdAsync(reqId);
                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                var currentStatus = req.StatusHistory.Last().ChangedStatus;
                var evidences = req.Evidences.Select(e => e.FilePath).ToList();
                var historyLines = req.StatusHistory.Select(s => $"{s.ChangedStatus} @ {s.ChangedAt:yyyy-MM-dd}");

                var body = $"ID:{req.Id} ユーザー:{req.UserId} 金額:{req.Amount.Value:C} 状態:{currentStatus} 日付:{req.RequestDate:yyyy-MM-dd}\n説明:{req.Description}\n" +
                           (evidences.Any() ? "証跡:\n" + string.Join("\n", evidences) + "\n" : "") +
                           "履歴:\n" + string.Join("\n", historyLines);

                await RespondAsync(body);
            }
            catch (Exception ex)
            {
                await RespondAsync($"申請詳細の取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("cancel-request", "確認待ち状態の申請を取り消す")]
        public async Task CancelRequest([Summary("request-id")] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var reqId))
                {
                    await RespondAsync($"申請IDは数値で指定してください: {requestId}", ephemeral: true);
                    return;
                }

                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。", ephemeral: true);
                    return;
                }

                var (req, groupId) = await _requestDetailUseCase.GetByIdAsync(reqId);
                if (req == null || groupId == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                // If the request owner wants to cancel a Pending request, allow it
                var isRequestOwner = req.UserId == user.Id;
                var currentStatus = req.StatusHistory.Last().ChangedStatus;
                if (isRequestOwner && currentStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending)
                {
                    await _userCancelRequestUseCase.ExecuteAsync(groupId.Value, reqId, user.Id);
                    await RespondAsync($"申請 {reqId} を申請者が取消しました。", ephemeral: true);
                    return;
                }

                // Otherwise, require privileged role to cancel (admin/accountant/etc.)
                var isPrivilegedCancel = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivilegedCancel)
                {
                    await RespondAsync("エラー: 申請取消の権限がありません。", ephemeral: true);
                    return;
                }

                await _cancelBudgetRequestUseCase.ExecuteAsync(groupId.Value, reqId, user.Id);

                await RespondAsync($"申請 {reqId} を取消しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"申請取消中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
