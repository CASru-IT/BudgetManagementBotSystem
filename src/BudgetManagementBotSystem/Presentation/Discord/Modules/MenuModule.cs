using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class MenuModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly UserQueryUseCase _userQuery;

        public MenuModule(UserQueryUseCase userQuery)
        {
            _userQuery = userQuery;
        }

        [SlashCommand("menu", "利用可能なコマンドを表示します")]
        public async Task Menu()
        {
            try
            {
                var user = await _userQuery.GetByDiscordIdAsync(Context.User.Id);
                if (user == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。管理者に登録を依頼してください。"), ephemeral: true);
                    return;
                }

                await RespondAsync(embed: DiscordEmbedFactory.BuildMenuEmbed(user), ephemeral: true);
            }
            catch (Exception)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("メニューを表示できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
