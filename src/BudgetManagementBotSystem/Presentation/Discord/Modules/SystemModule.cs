using BudgetManagementBotSystem.Application.UseCases;
using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class SystemModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BootstrapAdminUseCase _bootstrapAdminUseCase;

        public SystemModule(BootstrapAdminUseCase bootstrapAdminUseCase)
        {
            _bootstrapAdminUseCase = bootstrapAdminUseCase;
        }

        [SlashCommand("settings", "システム全体の設定を変更する")]
        public async Task Settings() => await RespondAsync("未実装: 設定");

        [SlashCommand("become-admin", "パスワードで自分を管理者に昇格する")]
        public async Task BecomeAdmin([Summary("password")] string password)
        {
            try
            {
                await _bootstrapAdminUseCase.ExecuteAsync(Context.User.Id, Context.User.Username, password);
                await RespondAsync("管理者権限を有効化しました。", ephemeral: true);
            }
            catch (ArgumentException ex)
            {
                await RespondAsync($"入力エラー: {ex.Message}", ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync("パスワードが正しくありません。", ephemeral: true);
            }
        }

        [SlashCommand("audit-log", "操作履歴や変更履歴を確認する")]
        public async Task AuditLog() => await RespondAsync("未実装: 監査ログ");

        [SlashCommand("backup", "データベースや設定をバックアップする")]
        public async Task Backup() => await RespondAsync("未実装: バックアップ");

        [SlashCommand("maintenance", "メンテナンスモードを切り替える")]
        public async Task MaintenanceMode() => await RespondAsync("未実装: メンテナンス");
    }
}
