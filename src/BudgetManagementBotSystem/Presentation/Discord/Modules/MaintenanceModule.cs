using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class MaintenanceModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("maintenance-backup", "データベースや設定をバックアップする（補助）")]
        public async Task Backup() => await RespondAsync("未実装: バックアップ（Maintenance）");

        [SlashCommand("toggle-maintenance", "メンテナンスモード切替（補助）")]
        public async Task ToggleMaintenance() => await RespondAsync("未実装: メンテナンス切替");
    }
}
