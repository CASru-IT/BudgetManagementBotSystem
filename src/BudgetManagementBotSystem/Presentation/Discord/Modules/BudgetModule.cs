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
        private readonly AdminAddBudgetTransactionUseCase _adminAddBudgetTransactionUseCase;

        public BudgetModule(
            IUserRepository userRepository,
            BudgetQueryUseCase budgetQueryUseCase,
            IncreaseBudgetLimitUseCase increaseBudgetLimitUseCase,
            AdminAddBudgetTransactionUseCase adminAddBudgetTransactionUseCase)
        {
            _userRepository = userRepository;
            _budgetQueryUseCase = budgetQueryUseCase;
            _increaseBudgetLimitUseCase = increaseBudgetLimitUseCase;
            _adminAddBudgetTransactionUseCase = adminAddBudgetTransactionUseCase;
        }

        [SlashCommand("remaining-budget", "現在の残予算を確認する")]
        public async Task RemainingBudget(int groupId, [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var discordUserId = Context.User.Id;
                try
                {
                    var dto = await _budgetQueryUseCase.GetRemainingBudgetAsync(discordUserId, groupId, fiscalYear);
                    await RespondAsync($"班:{dto.GroupName} 実残高:{dto.ActualBalance:C} 未承認合計:{dto.PendingTotal:C} 申請考慮後:{dto.AvailableAfterPending:C} 会計年度:{(fiscalYear?.ToString() ?? dto.FiscalYear.ToString())}");
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
        public async Task UsageHistory( int groupId, int page = 1, int pageSize = 10, [Summary("fiscal-year")] int? fiscalYear = null)
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
                var header = $"取引履歴 (ページ {result.Page}/{Math.Max(1, (int)Math.Ceiling(result.Total/(double)result.PageSize))}) 合計:{result.Total} 会計年度:{(fiscalYear?.ToString() ?? result.ResolvedFiscalYear.ToString())}";
                await RespondAsync($"{header}\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                await RespondAsync($"使用履歴取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
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

                var canAddBudget = await Helpers.AuthorizationHelper.IsPrivilegedAsync(_userRepository, discordUserId, BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin, BudgetManagementBotSystem.Domain.Enums.AccountRole.President);
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

                var remainingBudget = await _budgetQueryUseCase.GetRemainingBudgetAsync(discordUserId, groupId, fiscalYear);

                await RespondAsync($"班 {groupId} に {decAmount:C} を追加して、実残高 {remainingBudget.ActualBalance:C} となりました。", ephemeral: true);
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

        [SlashCommand("admin-add-transaction", "管理者用: 予算取引を直接追加する")]
        public async Task AdminAddTransaction(
            [Summary("group-id")] int groupId,
            [Summary("type"), Choice("収入", "income"), Choice("支出", "expense")] string transactionType,
            [Summary("amount")] double amount,
            [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var result = await _adminAddBudgetTransactionUseCase.ExecuteAsync(
                    Context.User.Id,
                    groupId,
                    transactionType,
                    Convert.ToDecimal(amount),
                    fiscalYear);

                var label = result.IsIncome ? "収入" : "支出";
                await RespondAsync(
                    $"予算取引を追加しました。班:{result.GroupName} 種別:{label} 金額:{result.Amount:C} 会計年度:{result.FiscalYear} 実残高:{result.ActualBalance:C}",
                    ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync("エラー: この操作には管理者権限が必要です。", ephemeral: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
            catch (ArgumentException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
            catch (InvalidOperationException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"予算取引追加中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
