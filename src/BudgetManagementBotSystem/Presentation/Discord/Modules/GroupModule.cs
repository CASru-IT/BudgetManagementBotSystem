using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterGroupUseCase _registerGroupUseCase;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;

        public GroupModule(
            RegisterGroupUseCase registerGroupUseCase,
            IGroupRepository groupRepository,
            IUserRepository userRepository)
        {
            _registerGroupUseCase = registerGroupUseCase;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
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
            var discordUserId = Context.User.Id;
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null)
            {
                await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                return;
            }

            if (user.Role != AccountRole.Admin)
            {
                await RespondAsync("エラー: このコマンドは管理者のみ実行できます。", ephemeral: true);
                return;
            }

            var groups = await _groupRepository.GetAllAsync();
            if (groups == null || groups.Count == 0)
            {
                await RespondAsync("登録済みの班はありません。", ephemeral: true);
                return;
            }

            var lines = groups
                .OrderBy(group => group.Id)
                .Select(group => $"班名: {group.Name} / 班ID: {group.Id}");

            await RespondAsync($"班一覧\n{string.Join("\n", lines)}", ephemeral: true);
        }

        [SlashCommand("delete-group", "班を削除または無効化する")]
        public async Task DeleteGroup() => await RespondAsync("未実装: 班削除");
    }
}
