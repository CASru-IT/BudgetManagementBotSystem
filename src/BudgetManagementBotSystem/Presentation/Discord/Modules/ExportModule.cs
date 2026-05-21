using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ExportModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("export-csv", "履歴や予算情報をCSV形式で出力する")]
        public async Task ExportCsv([Summary("対象")] string target) => await RespondAsync($"未実装: CSV出力 {target}");
    }
}
