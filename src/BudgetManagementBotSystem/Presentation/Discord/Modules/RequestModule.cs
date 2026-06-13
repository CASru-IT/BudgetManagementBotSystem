using Discord;
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

        public RequestModule(SubmitBudgetRequestUseCase submitBudgetRequestUseCase, CancelBudgetRequestUseCase cancelBudgetRequestUseCase, UserCancelBudgetRequestUseCase userCancelRequestUseCase, IUserRepository userRepository, BudgetManagementBotSystem.InfraStructure.Discord.DiscordBotService discordBotService, RequestListUseCase requestListUseCase, RequestDetailUseCase requestDetailUseCase, RequestQueryUseCase requestQueryUseCase)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _cancelBudgetRequestUseCase = cancelBudgetRequestUseCase;
            _userCancelRequestUseCase = userCancelRequestUseCase;
            _userRepository = userRepository;
            _discordBotService = discordBotService;
            _requestListUseCase = requestListUseCase;
            _requestDetailUseCase = requestDetailUseCase;
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
                if (uploaded == null || !uploaded.Any() || uploaded.Count < attachCount)
                {
                    await FollowupAsync("証跡ファイルの受け取りに失敗しました。申請を中止します。", ephemeral: true);
                    return;
                }

                evidenceFiles.AddRange(uploaded);
                await FollowupAsync("証跡ファイルを受け取りました。保存を開始します。", ephemeral: true);

                var (requestId, savedEvidenceCount) = await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, evidenceFiles);

                await FollowupAsync($"申請を作成しました: 班 {groupId} 金額 {amountDec:C}", ephemeral: true);
                if (savedEvidenceCount > 0)
                {
                    await FollowupAsync($"証跡ファイルの保存に成功しました: {savedEvidenceCount}件", ephemeral: true);
                }

                var notifiedCount = await NotifyAccountantsAsync(requestId, groupId, amountDec, description, user.Name, user.DiscordUserId);
                if (notifiedCount > 0)
                {
                    await FollowupAsync($"会計担当者 {notifiedCount} 名に DM で通知しました。", ephemeral: true);
                }
                else
                {
                    await FollowupAsync("会計担当者が見つからなかったため、DM 通知は送信されませんでした。", ephemeral: true);
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

        private async Task<int> NotifyAccountantsAsync(int requestId, int groupId, decimal amount, string description, string requesterName, ulong requesterDiscordUserId)
        {
            var users = await _userRepository.GetAllAsync();
            if (users == null)
            {
                return 0;
            }

            var accountantUsers = users
                .Where(user => user.IsActive && user.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant)
                .ToList();

            if (accountantUsers.Count == 0)
            {
                return 0;
            }

            var message = $"新しい予算使用申請が作成されました。\n申請ID: {requestId}\n班ID: {groupId}\n申請者: {requesterName}\n申請者DiscordID: {requesterDiscordUserId}\n金額: {amount:C}\n説明: {description}\n確認するには /request-detail request-id:{requestId} を実行してください。";

            var sendTasks = accountantUsers.Select(async accountant =>
            {
                return await _discordBotService.SendDirectMessageAsync(accountant.DiscordUserId, message);
            });

            var results = await Task.WhenAll(sendTasks);
            return results.Count(result => result);
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

                var detail = await _requestDetailUseCase.GetByIdAsync(reqId);
                var req = detail.Request;
                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                var currentStatus = req.GetCurrentStatus();
                var evidences = detail.Evidences;
                var historyLines = req.GetOrderedStatusHistory().Select(s => $"{s.ChangedStatus} @ {s.ChangedAt:yyyy-MM-dd}");

                var groupLabel = detail.GroupName ?? (detail.GroupId.HasValue ? detail.GroupId.Value.ToString() : "不明");

                var requesterName = string.IsNullOrWhiteSpace(detail.RequesterName) ? "不明" : detail.RequesterName;
                var requesterDiscordId = detail.RequesterDiscordUserId?.ToString() ?? "不明";
                var evidenceText = evidences.Any()
                    ? string.Join("\n", evidences.Select(e => e.FileName))
                    : "なし";
                var historyText = string.Join("\n", historyLines);
                if (string.IsNullOrWhiteSpace(historyText))
                {
                    historyText = "なし";
                }

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"申請詳細 #{req.Id}")
                    .WithColor(Color.Blue)
                    .AddField("班", groupLabel, true)
                    .AddField("金額", req.Amount.Value.ToString("C"), true)
                    .AddField("状態", currentStatus.ToString(), true)
                    .AddField("申請日", req.RequestDate.ToString("yyyy-MM-dd"), true)
                    .AddField("申請者", requesterName, true)
                    .AddField("申請者DiscordID", requesterDiscordId, true)
                    .AddField("説明", req.Description)
                    .AddField("証跡", evidenceText)
                    .AddField("履歴", historyText);

                if (detail.MissingEvidencePaths.Any())
                {
                    embedBuilder.AddField("添付できない証跡", string.Join("\n", detail.MissingEvidencePaths));
                }

                var embed = embedBuilder.Build();

                if (!evidences.Any())
                {
                    await RespondAsync(embed: embed);
                    return;
                }

                var files = new List<FileAttachment>();
                foreach (var evidence in evidences)
                {
                    files.Add(new FileAttachment(new MemoryStream(evidence.Content, writable: false), evidence.FileName));
                }

                await RespondWithFilesAsync(files, embeds: new[] { embed });
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

                var detail = await _requestDetailUseCase.GetByIdAsync(reqId);
                var req = detail.Request;
                var groupId = detail.GroupId;
                if (req == null || groupId == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                var isRequestOwner = req.UserId == user.Id;
                var currentStatus = req.GetCurrentStatus();
                if (isRequestOwner && currentStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending)
                {
                    await _userCancelRequestUseCase.ExecuteAsync(groupId.Value, reqId, user.Id);
                    await RespondAsync($"申請 {reqId} を申請者が取消しました。", ephemeral: true);
                    return;
                }

                var isPrivilegedCancel = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(
                    _userRepository,
                    discordUserId,
                    BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin);
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
