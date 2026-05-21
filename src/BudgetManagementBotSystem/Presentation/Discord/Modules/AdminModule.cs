using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class AdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定する")]
        public async Task SetUserRole() => await RespondAsync("未実装: ユーザー権限設定");

        [SlashCommand("register-group", "新しい班を登録する")]
        public async Task RegisterGroup() => await RespondAsync("未実装: 班登録");

        [SlashCommand("delete-group", "班を削除または無効化する")]
        public async Task DeleteGroup() => await RespondAsync("未実装: 班削除");

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
