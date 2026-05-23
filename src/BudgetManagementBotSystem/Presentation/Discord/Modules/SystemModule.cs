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
    }
}
