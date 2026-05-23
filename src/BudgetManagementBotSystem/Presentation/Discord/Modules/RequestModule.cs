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
        private readonly IUserRepository _userRepository;
        private readonly BudgetManagementDbContext _dbContext;

        public RequestModule(SubmitBudgetRequestUseCase submitBudgetRequestUseCase, IUserRepository userRepository, BudgetManagementDbContext dbContext)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _userRepository = userRepository;
            _dbContext = dbContext;
        }

        [SlashCommand("officer-request", "役員会用の予算申請を行う")]
        public async Task OfficerRequest() => await RespondAsync("未実装: 役員会申請");

        [SlashCommand("create-request", "予算使用申請を作成する")]
        public async Task CreateRequest(
            [Summary("班ID")] int groupId,
            [Summary("金額（例: 1234.56）")] double amount,
            [Summary("用途説明")] string description)
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

                await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, Enumerable.Empty<string>());

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
            [Summary("状態（任意）")] string status = null,
            [Summary("ページ番号(1-)")] int page = 1,
            [Summary("ページサイズ(最大50)")] int pageSize = 10,
            [Summary("班ID（役員専用、任意）")] int? groupId = null)
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

                var query = _dbContext.BudgetRequests
                    .Include(r => r.StatusHistory)
                    .Include(r => r.Evidences)
                    .AsQueryable();

                // 権限に応じた範囲制限
                if (user.Role == AccountRole.Admin || user.Role == AccountRole.Accountant)
                {
                    if (groupId.HasValue)
                    {
                        query = query.Where(r => EF.Property<int>(r, "GroupId") == groupId.Value);
                    }
                }
                else
                {
                    query = query.Where(r => EF.Property<int>(r, "GroupId") == user.GroupId);
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
        public async Task RequestDetail([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請詳細 {requestId}");

        [SlashCommand("cancel-request", "確認待ち状態の申請を取り消す")]
        public async Task CancelRequest([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請取消 {requestId}");

        [SlashCommand("reapply", "過去の申請内容をコピーして再申請する")]
        public async Task Reapply([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 再申請 {requestId}");

        [SlashCommand("expired-requests", "長期間未処理の申請を表示する")]
        public async Task ExpiredRequests() => await RespondAsync("未実装: 期限切れ申請");
    }
}
