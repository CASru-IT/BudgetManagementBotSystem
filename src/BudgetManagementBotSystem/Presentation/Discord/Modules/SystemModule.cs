using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class SystemModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("settings", "システム全体の設定を変更する")]
        public async Task Settings() => await RespondAsync("未実装: 設定");

        [SlashCommand("audit-log", "操作履歴や変更履歴を確認する")]
        public async Task AuditLog() => await RespondAsync("未実装: 監査ログ");

        [SlashCommand("backup", "データベースや設定をバックアップする")]
        public async Task Backup() => await RespondAsync("未実装: バックアップ");

        [SlashCommand("maintenance", "メンテナンスモードを切り替える")]
        public async Task MaintenanceMode() => await RespondAsync("未実装: メンテナンス");
    }
}
