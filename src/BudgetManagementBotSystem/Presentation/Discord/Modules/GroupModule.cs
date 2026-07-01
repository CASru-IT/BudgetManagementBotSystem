using BudgetManagementBotSystem.Application.UseCases.Groups;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterGroupUseCase _registerGroupUseCase;
        private readonly DeleteGroupUseCase _deleteGroupUseCase;
        private readonly ListGroupsUseCase _listGroupsUseCase;
        private readonly ILogger<GroupModule> _logger;

        public GroupModule(
            RegisterGroupUseCase registerGroupUseCase,
            DeleteGroupUseCase deleteGroupUseCase,
            ListGroupsUseCase listGroupsUseCase,
            ILogger<GroupModule> logger)
        {
            _registerGroupUseCase = registerGroupUseCase;
            _deleteGroupUseCase = deleteGroupUseCase;
            _listGroupsUseCase = listGroupsUseCase;
            _logger = logger;
        }

        [SlashCommand("register-group", "新しい班を登録します")]
        public async Task RegisterGroup([Summary("group-name")] string name)
        {
            try
            {
                await _registerGroupUseCase.ExecuteAsync(name);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("班を登録しました", $"班 `{name}` を登録しました。\n次に `/register-user` と `/assign-group` でユーザーを班に所属させてください。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("班名を確認してください。既に登録済みの班名は使用できません。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register group. DiscordUserId: {DiscordUserId}", Context.User.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班を登録できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("list-groups", "登録済みの班一覧を表示します")]
        public async Task ListGroups()
        {
            try
            {
                var groups = await _listGroupsUseCase.ExecuteAsync(Context.User.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildGroupListEmbed(groups), ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("班一覧を表示する権限がありません。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list groups. DiscordUserId: {DiscordUserId}", Context.User.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班一覧を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("delete-group", "班を削除または無効化します")]
        public async Task DeleteGroup([Summary("group-id"), Autocomplete(typeof(GroupAutocompleteHandler))] int groupId)
        {
            try
            {
                var groupName = await _deleteGroupUseCase.ExecuteAsync(Context.User.Id, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("班を削除しました", $"班 `{groupName}` (`{groupId}`) を削除または無効化しました。"), ephemeral: true);
            }
            catch (UnauthorizedAccessException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("このコマンドは管理者のみ実行できます。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("班", groupId.ToString()), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete group. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}", Context.User.Id, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班を削除できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }
    }
}
