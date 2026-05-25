using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterGroupUseCase _registerGroupUseCase;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly BudgetManagementDbContext _dbContext;

        public GroupModule(
            RegisterGroupUseCase registerGroupUseCase,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            BudgetManagementDbContext dbContext)
        {
            _registerGroupUseCase = registerGroupUseCase;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _dbContext = dbContext;
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
        public async Task DeleteGroup([Summary("group-id")] int groupId)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var caller = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (caller == null)
                {
                    await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                    return;
                }

                if (caller.Role != AccountRole.Admin)
                {
                    await RespondAsync("エラー: このコマンドは管理者のみ実行できます。", ephemeral: true);
                    return;
                }

                var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                {
                    await RespondAsync($"エラー: 指定された班が見つかりません: {groupId}", ephemeral: true);
                    return;
                }

                // 別テーブルの参照整合性を保つため、班に所属するユーザーの GroupId を null にする
                var members = await _dbContext.Users.Where(u => u.GroupId == groupId).ToListAsync();
                foreach (var m in members)
                {
                    m.ChangeGroupId(null);
                }

                // 名前に削除マークを付与して残す（物理削除は慎重に）
                var oldName = group.Name;
                var newName = $"{oldName} (deleted:{group.Id})";
                // Reflection because Name has private setter; use EF entry to set property
                _dbContext.Entry(group).Property(g => g.Name).CurrentValue = newName;

                await _dbContext.SaveChangesAsync();

                await RespondAsync($"班 {oldName} ({groupId}) を無効化しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"班削除中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
