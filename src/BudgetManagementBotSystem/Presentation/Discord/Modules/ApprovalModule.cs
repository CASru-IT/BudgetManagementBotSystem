using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ApprovalModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ApproveBudgetRequestUseCase _approveUseCase;
        private readonly RejectBudgetRequestUseCase _rejectUseCase;
        private readonly IUserRepository _userRepository;
        private readonly BudgetManagementDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public ApprovalModule(
            ApproveBudgetRequestUseCase approveUseCase,
            RejectBudgetRequestUseCase rejectUseCase,
            IUserRepository userRepository,
            BudgetManagementDbContext dbContext,
            IConfiguration configuration)
        {
            _approveUseCase = approveUseCase;
            _rejectUseCase = rejectUseCase;
            _userRepository = userRepository;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [SlashCommand("pending-list", "未承認の申請一覧を表示する")]
        public async Task PendingList(int page = 1, int pageSize = 10)
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
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!user.GroupId.HasValue && !isPrivileged)
                {
                    await RespondAsync("エラー: 班が未設定のため、未承認申請を表示できません。", ephemeral: true);
                    return;
                }

                var query = _dbContext.BudgetRequests
                    .Include(r => r.StatusHistory)
                    .Include(r => r.Evidences)
                    .Where(r => r.StatusHistory.Last().ChangedStatus == Domain.Enums.RequestStatus.Pending)
                    .AsQueryable();

                if (!isPrivileged)
                {
                    query = query.Where(r => EF.Property<int>(r, "GroupId") == user.GroupId!.Value);
                }

                var total = await query.CountAsync();
                var items = await query.OrderByDescending(r => r.RequestDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                if (!items.Any())
                {
                    await RespondAsync("未承認の申請は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = items.Select(r =>
                {
                    var currentStatus = r.StatusHistory.Last().ChangedStatus;
                    return $"ID:{r.Id} 金額:{r.Amount.Value:C} 日付:{r.RequestDate:yyyy-MM-dd} 説明:{(r.Description.Length>80? r.Description.Substring(0,80)+"...": r.Description)}";
                });

                var header = $"未承認申請一覧 (ページ {page}/{Math.Max(1, (int)Math.Ceiling(total/(double)pageSize))}) 合計:{total}";
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
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

                var req = await _dbContext.BudgetRequests.FirstOrDefaultAsync(r => r.Id == requestId);
                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {requestId}", ephemeral: true);
                    return;
                }

                int groupId = _dbContext.Entry(req).Property<int>("GroupId").CurrentValue;

                await _approveUseCase.ExecuteAsync(groupId, requestId, user.Id);
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
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

                var req = await _dbContext.BudgetRequests.FirstOrDefaultAsync(r => r.Id == requestId);
                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {requestId}", ephemeral: true);
                    return;
                }

                int groupId = _dbContext.Entry(req).Property<int>("GroupId").CurrentValue;

                await _rejectUseCase.ExecuteAsync(groupId, requestId, user.Id);
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

                var req = await _dbContext.BudgetRequests
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == reqId);

                if (req == null)
                {
                    await RespondAsync($"申請が見つかりません: {reqId}", ephemeral: true);
                    return;
                }

                var currentStatus = req.StatusHistory.Last().ChangedStatus;
                if (currentStatus != BudgetManagementBotSystem.Domain.Enums.RequestStatus.Approved)
                {
                    await RespondAsync($"申請 {reqId} は承認済みではありません。現在の状態: {currentStatus}", ephemeral: true);
                    return;
                }

                var discordUserId = Context.User.Id;
                var actingUser = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (actingUser == null)
                {
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivileged)
                {
                    await RespondAsync("エラー: 承認取消の権限がありません。", ephemeral: true);
                    return;
                }

                req.UpdateStatus(BudgetManagementBotSystem.Domain.Enums.RequestStatus.ApprovalCancelled, actingUser);
                await _dbContext.SaveChangesAsync();

                await RespondAsync($"申請 {reqId} の承認を取り消しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"承認取消中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
