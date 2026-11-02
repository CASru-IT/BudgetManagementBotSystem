using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules;

public class BudgetCancellationModule(
    CancelBudgetAdditionUseCase useCase,
    BudgetCancellationConfirmationStore store,
    ILogger<BudgetCancellationModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("cancel-budget", "誤った予算追加を履歴IDで指定し、確認して取り消します")]
    public async Task CancelBudget(
        [Summary("transaction-id", "/budget-history に表示される追加取引のID"), Autocomplete(typeof(BudgetTransactionAutocompleteHandler))] int transactionId,
        [Summary("reason", "取り消す理由（1〜500文字）")] string reason)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var preview = await useCase.PreviewAsync(Context.User.Id, transactionId, reason);
            var token = store.Create(Context.User.Id, preview);
            await FollowupAsync(embed: BudgetCancellationEmbeds.Confirmation(preview),
                components: BudgetCancellationEmbeds.Buttons(token), ephemeral: true,
                allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: Error(ex), ephemeral: true);
        }
    }

    [ComponentInteraction("budget:cancel:confirm:*")]
    public async Task ConfirmCancellation(string token) => await FinishAsync(token, true);

    [ComponentInteraction("budget:cancel:abort:*")]
    public async Task AbortCancellation(string token) => await FinishAsync(token, false);

    private async Task FinishAsync(string token, bool confirm)
    {
        await DeferAsync(ephemeral: true);
        if (!store.TryTake(token, Context.User.Id, out var item) || item == null)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("この確認は利用できません",
                "確認期限切れ・処理済み、または操作した本人ではありません。必要なら /cancel-budget を再実行してください。"), ephemeral: true);
            return;
        }

        Embed result;
        try
        {
            result = confirm
                ? BudgetCancellationEmbeds.Completed(await useCase.ExecuteAsync(Context.User.Id,
                    item.Preview.TransactionId, item.Preview.Reason, item.Preview.Revision))
                : DiscordEmbedFactory.BuildInfoEmbed("予算取消をキャンセルしました", "予算は変更していません。");
        }
        catch (Exception ex)
        {
            result = Error(ex);
        }

        await ModifyOriginalResponseAsync(message =>
        {
            message.Embed = result;
            message.Components = new ComponentBuilder().Build();
            message.AllowedMentions = AllowedMentions.None;
        });
    }

    private Embed Error(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return DiscordEmbedFactory.BuildAuthorizationErrorEmbed(ex.Message);
        if (ex is DbUpdateConcurrencyException || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
            return DiscordEmbedFactory.BuildWarningEmbed("予算が更新されています", "他の操作が先に反映されました。/budget-history を確認し、必要なら /cancel-budget を再実行してください。");
        if (ex is BudgetCancellationValidationException or BudgetCancellationRejectedException)
            return DiscordEmbedFactory.BuildValidationErrorEmbed(ex.Message);
        logger.LogError(ex, "Failed to cancel budget addition. DiscordUserId: {DiscordUserId}", Context.User.Id);
        return DiscordEmbedFactory.BuildErrorEmbed("予算取消を処理できません", "履歴で結果を確認してください。解決しない場合は管理者に連絡してください。");
    }
}
