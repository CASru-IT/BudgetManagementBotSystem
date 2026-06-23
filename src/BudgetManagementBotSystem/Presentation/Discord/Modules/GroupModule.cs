using Discord;
using Discord.Interactions;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.Groups;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterGroupUseCase _registerGroupUseCase;
        private readonly DeleteGroupUseCase _deleteGroupUseCase;
        private readonly ListGroupsUseCase _listGroupsUseCase;

        public GroupModule(
            RegisterGroupUseCase registerGroupUseCase,
            DeleteGroupUseCase deleteGroupUseCase,
            ListGroupsUseCase listGroupsUseCase)
        {
            _registerGroupUseCase = registerGroupUseCase;
            _deleteGroupUseCase = deleteGroupUseCase;
            _listGroupsUseCase = listGroupsUseCase;
        }

        [SlashCommand("register-group", "新しい班を登録する")]
        public async Task RegisterGroup([Summary("group-name")] string name)
        {
            await _registerGroupUseCase.ExecuteAsync(name);
            await RespondAsync($"班を登録しました: {name}");
        }

        [SlashCommand("list-groups", "登録済みの班一覧を表示する")]
        public async Task ListGroups()
        {
            try
            {
                var groups = await _listGroupsUseCase.ExecuteAsync(Context.User.Id);
                await RespondAsync(embed: BuildGroupListEmbed(groups), ephemeral: true);
            }
            catch (ArgumentException ex)
            {
                await RespondAsync(embed: BuildListGroupsErrorEmbed(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("delete-group", "班を削除または無効化する")]
        public async Task DeleteGroup([Summary("group-id")] int groupId)
        {
            try
            {
                var discordUserId = Context.User.Id;
                try
                {
                    var groupName = await _deleteGroupUseCase.ExecuteAsync(discordUserId, groupId);
                    await RespondAsync($"班 {groupName}（ID: {groupId}）を削除（無効化）しました。", ephemeral: true);
                }
                catch (UnauthorizedAccessException)
                {
                    await RespondAsync("エラー: このコマンドは管理者のみ実行できます。", ephemeral: true);
                }
                catch (ArgumentException ex)
                {
                    await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await RespondAsync($"班削除中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        private static Embed BuildGroupListEmbed(IEnumerable<GroupListItemDto> groups)
        {
            var orderedGroups = groups.ToList();

            var embed = new EmbedBuilder()
                .WithTitle("班一覧")
                .WithColor(Color.Blue)
                .AddField("登録班数", orderedGroups.Count.ToString(), true);

            if (orderedGroups.Count == 0)
            {
                return embed
                    .WithDescription("登録済みの班はありません。")
                    .Build();
            }

            foreach (var group in orderedGroups)
            {
                embed.AddField(
                    group.Name,
                    $"班ID: `{group.Id}`",
                    true);
            }

            return embed.Build();
        }

        private static Embed BuildListGroupsErrorEmbed(string message)
        {
            return new EmbedBuilder()
                .WithTitle("班一覧を表示できません")
                .WithColor(Color.Red)
                .WithDescription(message)
                .Build();
        }
    }
}
