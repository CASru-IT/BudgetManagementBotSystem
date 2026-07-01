using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
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

        [SlashCommand("become-admin", "パスワードで自分を管理者に昇格します")]
        public async Task BecomeAdmin([Summary("password")] string password)
        {
            try
            {
                await _bootstrapAdminUseCase.ExecuteAsync(Context.User.Id, Context.User.Username, password);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("管理者権限を有効化しました", "管理者向けコマンドを利用できます。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("入力内容を確認してください。"), ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("パスワードが正しくありません。"), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("管理者権限を有効化できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
