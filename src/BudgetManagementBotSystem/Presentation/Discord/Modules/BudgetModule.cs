using Discord.Interactions;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BudgetManagementBotSystem.Domain.Enums;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class BudgetModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IUserRepository _userRepository;
        private readonly BudgetManagementDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly BudgetManagementBotSystem.Application.UseCases.IncreaseBudgetLimitUseCase _increaseBudgetLimitUseCase;

        public BudgetModule(IUserRepository userRepository, BudgetManagementDbContext dbContext, IConfiguration configuration, BudgetManagementBotSystem.Application.UseCases.IncreaseBudgetLimitUseCase increaseBudgetLimitUseCase)
        {
            _userRepository = userRepository;
            _dbContext = dbContext;
            _configuration = configuration;
            _increaseBudgetLimitUseCase = increaseBudgetLimitUseCase;
        }

        [SlashCommand("remaining-budget", "現在の残予算を確認する")]
        public async Task RemainingBudget(int? groupId = null)
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

                if (!user.GroupId.HasValue && !groupId.HasValue)
                {
                    await RespondAsync("エラー: 班が未設定のため、残予算を表示できません。", ephemeral: true);
                    return;
                }

                int targetGroupId = groupId ?? user.GroupId!.Value;
                var isPrivileged = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivileged && groupId.HasValue && (!user.GroupId.HasValue || groupId.Value != user.GroupId.Value))
                {
                    await RespondAsync("エラー: 指定した班の情報を参照する権限がありません。", ephemeral: true);
                    return;
                }

                var group = await _dbContext.Groups
                    .Include(g => g.BudgetTransactions)
                    .Include(g => g.Requests)
                        .ThenInclude(r => r.StatusHistory)
                    .FirstOrDefaultAsync(g => g.Id == targetGroupId);

                if (group == null)
                {
                    await RespondAsync($"班が見つかりません: {targetGroupId}", ephemeral: true);
                    return;
                }

                int startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
                var fiscalYear = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(startMonth);

                decimal totalBudget = group.GetTotalBudgetForFiscalYear(fiscalYear);
                // 未承認の申請合計
                var pendingTotal = group.Requests
                    .Where(r => r.StatusHistory.Last().ChangedStatus == RequestStatus.Pending && r.FiscalYear == fiscalYear)
                    .Sum(r => r.Amount.Value);

                decimal available = totalBudget - pendingTotal;

                await RespondAsync($"班:{group.Name} 現在予算:{totalBudget:C} 未承認合計:{pendingTotal:C} 利用可能:{available:C}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"残予算取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("usage-history", "予算使用履歴を表示する")]
        public async Task UsageHistory(int page = 1, int pageSize = 10, int? groupId = null)
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

                if (!user.GroupId.HasValue && !groupId.HasValue)
                {
                    await RespondAsync("エラー: 班が未設定のため、使用履歴を表示できません。", ephemeral: true);
                    return;
                }

                int targetGroupId = groupId ?? user.GroupId!.Value;
                var isPrivileged2 = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!isPrivileged2 && groupId.HasValue && (!user.GroupId.HasValue || groupId.Value != user.GroupId.Value))
                {
                    await RespondAsync("エラー: 指定した班の情報を参照する権限がありません。", ephemeral: true);
                    return;
                }

                var query = _dbContext.BudgetTransactions
                    .Where(t => EF.Property<int>(t, "GroupId") == targetGroupId)
                    .OrderByDescending(t => t.TransactionDate)
                    .AsQueryable();

                var total = await query.CountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                if (!items.Any())
                {
                    await RespondAsync("予算取引の履歴は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = items.Select(t => $"{(t.IsIncome?"収入":"支出")} {t.Amount.Value:C} 日付:{t.TransactionDate:yyyy-MM-dd} 年度:{t.FiscalYear.Year}");
                var header = $"取引履歴 (ページ {page}/{Math.Max(1, (int)Math.Ceiling(total/(double)pageSize))}) 合計:{total}";
                await RespondAsync($"{header}\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"使用履歴取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("register-budget", "年度ごとの班予算を登録する")]
        public async Task RegisterBudget([Summary("group-id")] int groupId, [Summary("amount")] double amount)
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

                var canRegister = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!canRegister)
                {
                    await RespondAsync("エラー: 予算登録の権限がありません。", ephemeral: true);
                    return;
                }

                if (amount <= 0)
                {
                    await RespondAsync("エラー: 予算金額は正の数で指定してください。", ephemeral: true);
                    return;
                }

                var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                {
                    await RespondAsync($"班が見つかりません: {groupId}", ephemeral: true);
                    return;
                }

                int startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
                var fiscalYear = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(startMonth);

                decimal decAmount = Convert.ToDecimal(amount);
                var tx = new BudgetManagementBotSystem.Domain.Entities.BudgetTransaction(true, decAmount, fiscalYear);
                group.AddBudgetTransaction(tx);

                await _dbContext.SaveChangesAsync();

                await RespondAsync($"班 {group.Name} の年度予算を {decAmount:C} として登録しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"予算登録中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("add-budget", "追加予算を付与する")]
        public async Task AddBudget(int groupId, double amount)
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

                // Only Admins and Accountants are allowed to add budget
                var canAddBudget = await BudgetManagementBotSystem.Presentation.Discord.Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId);
                if (!canAddBudget)
                {
                    await RespondAsync("エラー: 追加予算の付与を行う権限がありません。", ephemeral: true);
                    return;
                }

                if (amount <= 0)
                {
                    await RespondAsync("エラー: 追加金額は正の数で指定してください。", ephemeral: true);
                    return;
                }

                decimal decAmount = Convert.ToDecimal(amount);

                await _increaseBudgetLimitUseCase.ExecuteAsync(groupId, decAmount);

                await RespondAsync($"班 {groupId} に {decAmount:C} の予算を追加しました。", ephemeral: true);
            }
            catch (ArgumentNullException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"追加予算処理中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("change-budget", "登録済み予算を修正する")]
        public async Task ChangeBudget() => await RespondAsync("未実装: 予算変更");

        [SlashCommand("create-year", "新年度データを作成する")]
        public async Task CreateYear() => await RespondAsync("未実装: 年度作成");

        [SlashCommand("low-budget-warnings", "残予算が少ない班を表示する")]
        public async Task LowBudgetWarnings()
        {
            try
            {
                int startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
                var fiscalYear = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(startMonth);

                var groups = await _dbContext.Groups
                    .Include(g => g.BudgetTransactions)
                    .Include(g => g.Requests)
                        .ThenInclude(r => r.StatusHistory)
                    .ToListAsync();

                var warnings = new List<string>();
                foreach (var g in groups)
                {
                    var total = g.GetTotalBudgetForFiscalYear(fiscalYear);
                    var pending = g.Requests.Where(r => r.StatusHistory.Last().ChangedStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending && r.FiscalYear == fiscalYear).Sum(r => r.Amount.Value);
                    var available = total - pending;
                    if (total == 0 || available <= 0 || available < total * 0.1m)
                    {
                        warnings.Add($"班:{g.Name} 予算:{total:C} 未承認合計:{pending:C} 利用可能:{available:C}");
                    }
                }

                if (!warnings.Any())
                {
                    await RespondAsync("残予算が少ない班は見つかりませんでした。", ephemeral: true);
                    return;
                }

                await RespondAsync(string.Join("\n", warnings));
            }
            catch (Exception ex)
            {
                await RespondAsync($"残予算警告取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("budget-ranking", "班ごとの予算使用率ランキングを表示する")]
        public async Task BudgetRanking(int top = 10)
        {
            try
            {
                int startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
                var fiscalYear = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(startMonth);

                var groups = await _dbContext.Groups
                    .Include(g => g.BudgetTransactions)
                    .Include(g => g.Requests)
                        .ThenInclude(r => r.StatusHistory)
                    .ToListAsync();

                var ranking = groups.Select(g =>
                {
                    var total = g.GetTotalBudgetForFiscalYear(fiscalYear);
                    var pending = g.Requests.Where(r => r.StatusHistory.Last().ChangedStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending && r.FiscalYear == fiscalYear).Sum(r => r.Amount.Value);
                    var available = total - pending;
                    decimal usedPercent = 0;
                    if (total > 0) usedPercent = Math.Clamp(1 - (available / total), 0, 1);
                    return new { Group = g, Total = total, Available = available, UsedPercent = usedPercent };
                })
                .OrderByDescending(x => x.UsedPercent)
                .Take(Math.Max(1, top))
                .ToList();

                var lines = ranking.Select((r, idx) => $"{idx + 1}. 班:{r.Group.Name} 使用率:{r.UsedPercent:P1} 予算:{r.Total:C} 残:{r.Available:C}");
                await RespondAsync(string.Join("\n", lines));
            }
            catch (Exception ex)
            {
                await RespondAsync($"予算ランキング取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("monthly-summary", "今月の支出状況を集計表示する")]
        public async Task MonthlySummary()
        {
            try
            {
                var now = DateTime.Now;
                var groups = await _dbContext.Groups
                    .Include(g => g.BudgetTransactions)
                    .ToListAsync();

                var lines = groups.Select(g =>
                {
                    var monthTx = g.BudgetTransactions.Where(t => t.TransactionDate.Year == now.Year && t.TransactionDate.Month == now.Month).ToList();
                    var income = monthTx.Where(t => t.IsIncome).Sum(t => t.Amount.Value);
                    var expense = monthTx.Where(t => !t.IsIncome).Sum(t => t.Amount.Value);
                    return $"班:{g.Name} 収入:{income:C} 支出:{expense:C} 件数:{monthTx.Count}";
                });

                await RespondAsync(string.Join("\n", lines));
            }
            catch (Exception ex)
            {
                await RespondAsync($"月次集計取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("all-history", "全班の予算使用履歴を閲覧する")]
        public async Task AllHistory(int take = 50)
        {
            try
            {
                var groups = await _dbContext.Groups
                    .Include(g => g.BudgetTransactions)
                    .ToListAsync();

                var allTx = groups.SelectMany(g => g.BudgetTransactions.Select(t => new { GroupName = g.Name, Tx = t }))
                    .OrderByDescending(x => x.Tx.TransactionDate)
                    .Take(Math.Max(1, take))
                    .ToList();

                if (!allTx.Any())
                {
                    await RespondAsync("取引履歴は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = allTx.Select(x => $"班:{x.GroupName} {(x.Tx.IsIncome?"収入":"支出")} {x.Tx.Amount.Value:C} 日付:{x.Tx.TransactionDate:yyyy-MM-dd} 年度:{x.Tx.FiscalYear.Year}");
                await RespondAsync(string.Join("\n", lines));
            }
            catch (Exception ex)
            {
                await RespondAsync($"全履歴取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
