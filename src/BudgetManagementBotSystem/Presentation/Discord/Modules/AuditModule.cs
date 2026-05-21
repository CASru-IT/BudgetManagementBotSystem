using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class AuditModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("show-audit", "操作履歴を確認する（補助）")]
        public async Task ShowAudit() => await RespondAsync("未実装: 監査ログ（Audit）");
    }
}
