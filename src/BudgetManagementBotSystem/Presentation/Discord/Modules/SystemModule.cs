using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class SystemModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BootstrapAdminUseCase _bootstrapAdminUseCase;
        private readonly ILogger<SystemModule> _logger;

        public SystemModule(
            BootstrapAdminUseCase bootstrapAdminUseCase,
            ILogger<SystemModule> logger)
        {
            _bootstrapAdminUseCase = bootstrapAdminUseCase;
            _logger = logger;
        }

        [SlashCommand("become-admin", "パスワードで自分を管理者に昇格します")]
        public async Task BecomeAdmin([Summary("password")] string password)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                await _bootstrapAdminUseCase.ExecuteAsync(Context.User.Id, Context.User.Username, password);
                await FollowupAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("管理者権限を有効化しました", "管理者向けコマンドを利用できます。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("入力内容を確認してください。"), ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await FollowupAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("パスワードが正しくありません。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bootstrap admin. DiscordUserId: {DiscordUserId}", Context.User.Id);
                await FollowupAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("管理者権限を有効化できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
