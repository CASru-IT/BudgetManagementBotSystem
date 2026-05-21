using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class UserManagementModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定する")]
        public async Task SetUserRole() => await RespondAsync("未実装: ユーザー権限設定");
    }
}
