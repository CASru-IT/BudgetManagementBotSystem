using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class OfficerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("officer-request", "役員会用の予算申請を行う")]
        public async Task OfficerRequest() => await RespondAsync("未実装: 役員会申請");
    }
}
