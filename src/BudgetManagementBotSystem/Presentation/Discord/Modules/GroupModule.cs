using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("register-group", "新しい班を登録する")]
        public async Task RegisterGroup() => await RespondAsync("未実装: 班登録");

        [SlashCommand("delete-group", "班を削除または無効化する")]
        public async Task DeleteGroup() => await RespondAsync("未実装: 班削除");
    }
}
