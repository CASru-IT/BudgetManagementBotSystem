using Discord.Interactions;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Application.UseCases.Budget;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class BudgetModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IUserRepository _userRepository;
        private readonly BudgetQueryUseCase _budgetQueryUseCase;
        private readonly IncreaseBudgetLimitUseCase _increaseBudgetLimitUseCase;

        public BudgetModule(IUserRepository userRepository, BudgetQueryUseCase budgetQueryUseCase, IncreaseBudgetLimitUseCase increaseBudgetLimitUseCase)
        {
            _userRepository = userRepository;
            _budgetQueryUseCase = budgetQueryUseCase;
            _increaseBudgetLimitUseCase = increaseBudgetLimitUseCase;
        }

        [SlashCommand("remaining-budget", "現在の残予算を確認する")]
        public async Task RemainingBudget(int? groupId = null, [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var discordUserId = Context.User.Id;
                try
                {
                    var dto = await _budgetQueryUseCase.GetRemainingBudgetAsync(discordUserId, groupId, fiscalYear);
                    await RespondAsync($"班:{dto.GroupName} 現在予算:{dto.TotalBudget:C} 未承認合計:{dto.PendingTotal:C} 利用可能:{dto.Available:C} 会計年度:{fiscalYear?.ToString() ?? "自動"}");
                }
                catch (Exception ex)
                {
                    await RespondAsync($"残予算取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await RespondAsync($"残予算取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("usage-history", "予算使用履歴を表示する")]
        public async Task UsageHistory(int page = 1, int pageSize = 10, int? groupId = null, [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                pageSize = Math.Min(pageSize, 50);

                var discordUserId = Context.User.Id;
                var result = await _budgetQueryUseCase.GetUsageHistoryAsync(discordUserId, page, pageSize, groupId, fiscalYear);

                if (result.Total == 0 || result.Items == null || !result.Items.Any())
                {
                    await RespondAsync("予算取引の履歴は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = result.Items.Select(t => $"{(t.IsIncome?"収入":"支出")} {t.Amount:C} 日付:{t.TransactionDate:yyyy-MM-dd} 年度:{t.FiscalYear}");
                var header = $"取引履歴 (ページ {result.Page}/{Math.Max(1, (int)Math.Ceiling(result.Total/(double)result.PageSize))}) 合計:{result.Total} 会計年度:{fiscalYear?.ToString() ?? "自動"}";
                await RespondAsync($"{header}\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"使用履歴取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("register-budget", "年度ごとの班予算を登録する")]
        public async Task RegisterBudget(
            [Summary("group-id")] int groupId,
            [Summary("amount")] double amount,
            [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var caller = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (caller == null)
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

                decimal decAmount = Convert.ToDecimal(amount);
                await _increaseBudgetLimitUseCase.ExecuteAsync(groupId, decAmount, fiscalYear);

                await RespondAsync($"班 {groupId} の年度予算を {decAmount:C} として登録しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"予算登録中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("add-budget", "追加予算を付与する")]
        public async Task AddBudget(
            int groupId,
            double amount,
            [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var caller = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (caller == null)
                {
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

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

                await _increaseBudgetLimitUseCase.ExecuteAsync(groupId, decAmount, fiscalYear);

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

        [SlashCommand("all-history", "全班の予算使用履歴を閲覧する")]
        public async Task AllHistory(int take = 50)
        {
            try
            {
                var allTx = await _budgetQueryUseCase.GetAllHistoryAsync(take);

                if (!allTx.Any())
                {
                    await RespondAsync("取引履歴は見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = allTx.Select(x => $"班:{x.GroupName} {(x.IsIncome?"収入":"支出")} {x.Amount:C} 日付:{x.TransactionDate:yyyy-MM-dd} 年度:{x.FiscalYear}");
                await RespondAsync(string.Join("\n", lines));
            }
            catch (Exception ex)
            {
                await RespondAsync($"全履歴取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
