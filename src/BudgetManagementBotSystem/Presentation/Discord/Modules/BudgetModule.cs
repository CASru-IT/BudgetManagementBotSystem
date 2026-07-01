using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord.Interactions;

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

        [SlashCommand("remaining-budget", "現在の残予算を確認します")]
        public async Task RemainingBudget(int groupId, [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var dto = await _budgetQueryUseCase.GetRemainingBudgetAsync(Context.User.Id, groupId, fiscalYear);
                await RespondAsync(embed: DiscordEmbedFactory.BuildRemainingBudgetEmbed(dto));
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この班の予算状況を確認する権限がありません。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("班", groupId.ToString()), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("予算状況を取得できません", "時間を置いて再実行してください。解決しない場合は管理者に連絡してください。"), ephemeral: true);
            }
        }

        [SlashCommand("usage-history", "予算使用履歴を表示します")]
        public async Task UsageHistory(int groupId, int page = 1, int pageSize = 10, [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Min(Math.Max(1, pageSize), 50);

                var result = await _budgetQueryUseCase.GetUsageHistoryAsync(Context.User.Id, page, pageSize, groupId, fiscalYear);
                await RespondAsync(embed: DiscordEmbedFactory.BuildUsageHistoryEmbed(result, fiscalYear), ephemeral: result.Total == 0);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この履歴を確認する権限がありません。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("使用履歴を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("add-budget", "追加予算を付与します")]
        public async Task AddBudget(
            int groupId,
            double amount,
            [Summary("fiscal-year")] int? fiscalYear = null)
        {
            try
            {
                var caller = await _userRepository.GetByDiscordUserIdAsync(Context.User.Id);
                if (caller == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。管理者に登録を依頼してください。"), ephemeral: true);
                    return;
                }

                var canAddBudget = await AuthorizationHelper.IsPrivilegedAsync(_userRepository, Context.User.Id, AccountRole.Admin, AccountRole.President);
                if (!canAddBudget)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("追加予算を付与する権限がありません。"), ephemeral: true);
                    return;
                }

                if (amount <= 0)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("追加金額は正の数で指定してください。"), ephemeral: true);
                    return;
                }

                var amountDecimal = Convert.ToDecimal(amount);
                await _increaseBudgetLimitUseCase.ExecuteAsync(groupId, amountDecimal, fiscalYear);
                var remainingBudget = await _budgetQueryUseCase.GetRemainingBudgetAsync(Context.User.Id, groupId, fiscalYear);

                await RespondAsync(
                    embed: DiscordEmbedFactory.BuildBudgetAddedEmbed(
                        remainingBudget.GroupName,
                        remainingBudget.GroupId,
                        amountDecimal,
                        remainingBudget.ActualBalance,
                        remainingBudget.FiscalYear),
                    ephemeral: true);
            }
            catch (ArgumentNullException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("班", groupId.ToString()), ephemeral: true);
            }
            catch (ArgumentOutOfRangeException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("追加金額は正の数で指定してください。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("追加予算を処理できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("all-history", "全班の予算使用履歴を閲覧します")]
        public async Task AllHistory(int take = 50)
        {
            try
            {
                var allTransactions = await _budgetQueryUseCase.GetAllHistoryAsync(take);
                await RespondAsync(embed: DiscordEmbedFactory.BuildAllHistoryEmbed(allTransactions, take), ephemeral: !allTransactions.Any());
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("全班履歴を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("admin-add-transaction", "管理者用: 予算取引を直接追加します")]
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

                await RespondAsync(
                    embed: DiscordEmbedFactory.BuildTransactionAddedEmbed(
                        result.GroupId,
                        result.GroupName,
                        result.IsIncome,
                        result.Amount,
                        result.FiscalYear,
                        result.ActualBalance),
                    ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この操作には管理者権限が必要です。"), ephemeral: true);
            }
            catch (ArgumentOutOfRangeException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("金額は正の数で指定してください。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("班ID、種別、金額を確認してください。"), ephemeral: true);
            }
            catch (InvalidOperationException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("この取引を追加すると予算条件を満たせません。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("予算取引を追加できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
