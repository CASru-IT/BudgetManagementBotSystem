using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules;

public class GroupLeaderModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("StartBudgetRequestWizard","グループリーダーが予算申請を行うコマンドです。")]
    public async Task BudgetRequestWizard()
    {
        await RespondAsync("予算申請ウィザードが開始されました。");
    }
}
