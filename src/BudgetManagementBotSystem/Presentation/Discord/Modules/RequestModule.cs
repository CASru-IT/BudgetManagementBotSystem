using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SubmitBudgetRequestUseCase _submitBudgetRequestUseCase;
        private readonly CancelBudgetRequestUseCase _cancelBudgetRequestUseCase;
        private readonly UserCancelBudgetRequestUseCase _userCancelRequestUseCase;
        private readonly IUserRepository _userRepository;
        private readonly BudgetManagementDbContext _dbContext;
        private readonly BudgetManagementBotSystem.InfraStructure.Discord.DiscordBotService _discordBotService;

        public RequestModule(SubmitBudgetRequestUseCase submitBudgetRequestUseCase, CancelBudgetRequestUseCase cancelBudgetRequestUseCase, UserCancelBudgetRequestUseCase userCancelRequestUseCase, IUserRepository userRepository, BudgetManagementDbContext dbContext, BudgetManagementBotSystem.InfraStructure.Discord.DiscordBotService discordBotService)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _cancelBudgetRequestUseCase = cancelBudgetRequestUseCase;
            _userCancelRequestUseCase = userCancelRequestUseCase;
            _userRepository = userRepository;
            _dbContext = dbContext;
            _discordBotService = discordBotService;
        }

        [SlashCommand("officer-request", "役員会用の予算申請を行う")]
        public async Task OfficerRequest(
            [Summary("group-id")] int groupId,
            [Summary("amount")] double amount,
            [Summary("description")] string description,
            [Summary("attach")] bool attach = false)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。", ephemeral: true);
                    return;
                }

                decimal amountDec = Convert.ToDecimal(amount);

                var tempPaths = new List<string>();
                if (attach)
                {
                    await RespondAsync("証跡ファイルをこのチャンネルに添付してください。30秒以内にアップロードしてください。", ephemeral: true);
                    var uploaded = await _discordBotService.WaitForAttachmentUploadAsync(Context.User.Id, TimeSpan.FromSeconds(30), Context.Channel);
                    if (uploaded != null && uploaded.Any())
                    {
                        tempPaths.AddRange(uploaded);
                    }
                }

                await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, tempPaths);

                await RespondAsync($"役員会申請を作成しました: 班 {groupId} 金額 {amountDec:C}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"役員会申請中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("create-request", "予算使用申請を作成する")]
        public async Task CreateRequest(
            [Summary("group-id")] int groupId,
            [Summary("amount")] double amount,
            [Summary("description")] string description,
            [Summary("attach")]
            bool attach = false)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。管理者に登録を依頼してください。", ephemeral: true);
                    return;
                }

                decimal amountDec = Convert.ToDecimal(amount);

                var tempPaths = new List<string>();
                if (attach)
                {
                    await RespondAsync("証跡ファイルをこのチャンネルに添付してください。30秒以内にアップロードしてください。", ephemeral: true);
                    var uploaded = await _discordBotService.WaitForAttachmentUploadAsync(Context.User.Id, TimeSpan.FromSeconds(30), Context.Channel);
                    if (uploaded != null && uploaded.Any())
                    {
                        tempPaths.AddRange(uploaded);
                    }
                }

                await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, tempPaths);

                await RespondAsync($"申請を作成しました: 班 {groupId} 金額 {amountDec:C}");
            }
            catch (ArgumentNullException ex)
            {
                await RespondAsync($"入力エラー: {ex.ParamName} - {ex.Message}", ephemeral: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                await RespondAsync($"入力エラー: {ex.Message}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"予期せぬエラーが発生しました: {ex.Message}", ephemeral: true);
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
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。", ephemeral: true);
                    return;
                }

                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!user.GroupId.HasValue && !isPrivileged)
                {
                    await RespondAsync("エラー: 班が未設定のため、申請一覧を表示できません。", ephemeral: true);
                    return;
                }

                var query = _dbContext.BudgetRequests
                    .Include(r => r.StatusHistory)
                    .Include(r => r.Evidences)
                    .AsQueryable();

                // 権限に応じた範囲制限（DB の Role を参照）
                if (isPrivileged)
                {
                    if (groupId.HasValue)
                    {
                        query = query.Where(r => EF.Property<int>(r, "GroupId") == groupId.Value);
                    }
                }
                else
                {
                    query = query.Where(r => EF.Property<int>(r, "GroupId") == user.GroupId!.Value);
                }

                query = query.OrderByDescending(r => r.RequestDate);

                var all = await query.ToListAsync();

                // 状態フィルタ（取得後に評価）
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Enum.TryParse<RequestStatus>(status, true, out var parsed))
                    {
                        all = all.Where(r => r.StatusHistory.Last().ChangedStatus == parsed).ToList();
                    }
                    else
                    {
                        await RespondAsync($"不正な状態フィルタです: {status}", ephemeral: true);
                        return;
                    }
                }

                var total = all.Count;
                var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                if (!items.Any())
                {
                    await RespondAsync("該当する申請は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = items.Select(r =>
                {
                    var currentStatus = r.StatusHistory.Last().ChangedStatus;
                    return $"ID:{r.Id} 金額:{r.Amount.Value:C} 状態:{currentStatus} 日付:{r.RequestDate:yyyy-MM-dd} 説明:{(r.Description.Length>80? r.Description.Substring(0,80)+"...": r.Description)}";
                });

                var header = $"申請一覧 (ページ {page}/{Math.Max(1, (int)Math.Ceiling(total/(double)pageSize))}) 合計:{total}";
                var body = string.Join("\n", lines);

                await RespondAsync($"{header}\n{body}");
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

                var req = await _dbContext.BudgetRequests
                    .Include(r => r.Evidences)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == reqId);

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

                var req = await _dbContext.BudgetRequests.FirstOrDefaultAsync(r => r.Id == reqId);
                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                int groupId = _dbContext.Entry(req).Property<int>("GroupId").CurrentValue;

                // If the request owner wants to cancel a Pending request, allow it
                var isRequestOwner = req.UserId == user.Id;
                var currentStatus = req.StatusHistory.Last().ChangedStatus;
                if (isRequestOwner && currentStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending)
                {
                    await _userCancelRequestUseCase.ExecuteAsync(groupId, reqId, user.Id);
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

                await _cancelBudgetRequestUseCase.ExecuteAsync(groupId, reqId, user.Id);

                await RespondAsync($"申請 {reqId} を取消しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"申請取消中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("reapply", "過去の申請内容をコピーして再申請する")]
        public async Task Reapply([Summary("request-id")] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var reqId))
                {
                    await RespondAsync($"申請IDは数値で指定してください: {requestId}", ephemeral: true);
                    return;
                }

                var req = await _dbContext.BudgetRequests
                    .Include(r => r.Evidences)
                    .FirstOrDefaultAsync(r => r.Id == reqId);

                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。", ephemeral: true);
                    return;
                }

                if (!user.GroupId.HasValue)
                {
                    await RespondAsync("エラー: 班が未設定のため、再申請できません。", ephemeral: true);
                    return;
                }

                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivileged && user.Id != req.UserId)
                {
                    await RespondAsync("エラー: 再申請の権限がありません。", ephemeral: true);
                    return;
                }

                int groupId = _dbContext.Entry(req).Property<int>("GroupId").CurrentValue;
                decimal amount = req.Amount.Value;
                string description = req.Description;
                var evidencePaths = req.Evidences.Select(e => e.FilePath).ToList();

                await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amount, description, evidencePaths);

                await RespondAsync($"申請 {reqId} を元に再申請を作成しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"再申請中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("expired-requests", "長期間未処理の申請を表示する")]
        public async Task ExpiredRequests(int days = 30)
        {
            try
            {
                if (days < 1) days = 30;

                var cutoff = DateTime.Now.AddDays(-days);

                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。", ephemeral: true);
                    return;
                }

                var query = _dbContext.BudgetRequests
                    .Include(r => r.StatusHistory)
                    .Where(r => r.RequestDate < cutoff)
                    .AsQueryable();

                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivileged)
                {
                    if (!user.GroupId.HasValue)
                    {
                        await RespondAsync("エラー: 班が未設定のため、期限切れ申請を表示できません。", ephemeral: true);
                        return;
                    }

                    query = query.Where(r => EF.Property<int>(r, "GroupId") == user.GroupId.Value);
                }

                var items = await query.OrderByDescending(r => r.RequestDate).Take(100).ToListAsync();
                if (!items.Any())
                {
                    await RespondAsync("期限切れ申請は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = items.Select(r =>
                {
                    var status = r.StatusHistory.Last().ChangedStatus;
                    return $"ID:{r.Id} 班ID:{_dbContext.Entry(r).Property<int>("GroupId").CurrentValue} 金額:{r.Amount.Value:C} 状態:{status} 日付:{r.RequestDate:yyyy-MM-dd} 説明:{(r.Description.Length>80? r.Description.Substring(0,80)+"...": r.Description)}";
                });

                await RespondAsync($"期限切れ申請（{days}日以上）\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"期限切れ申請取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
