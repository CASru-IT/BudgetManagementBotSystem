using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ReportingModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("search-purpose", "用途名で申請や履歴を検索する")]
        public async Task SearchByPurpose([Summary("キーワード")] string keyword) => await RespondAsync($"未実装: 用途検索 {keyword}");
    }
}
