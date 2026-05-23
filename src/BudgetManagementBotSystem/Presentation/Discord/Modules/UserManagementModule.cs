using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class UserManagementModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterUserUseCase _registerUserUseCase;
        private readonly BudgetManagementBotSystem.Domain.Repository.IUserRepository _userRepository;
        private readonly BudgetManagementBotSystem.InfraStructure.Persistence.BudgetManagementDbContext _dbContext;

        public UserManagementModule(RegisterUserUseCase registerUserUseCase, BudgetManagementBotSystem.Domain.Repository.IUserRepository userRepository, BudgetManagementBotSystem.InfraStructure.Persistence.BudgetManagementDbContext dbContext)
        {
            _registerUserUseCase = registerUserUseCase;
            _userRepository = userRepository;
            _dbContext = dbContext;
        }

        [SlashCommand("register-user", "システム利用ユーザーを登録する")]
        public async Task RegisterUser(
            [Summary("name")] string name,
            [Summary("discord-user-id")] string discordUserIdStr,
            [Summary("role")] AccountRole role)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            var existingUser = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (existingUser != null)
            {
                await RespondAsync("エラー: この Discord ユーザーは既に登録されています。", ephemeral: true);
                return;
            }

            await _registerUserUseCase.ExecuteAsync(name, discordUserId, role);
            await RespondAsync($"ユーザーを登録しました: {name} ({role})", ephemeral: true);
        }

        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定する")]
        public async Task SetUserRole(
            [Summary("discord-user-id")] string discordUserIdStr,
            [Summary("role")] AccountRole role)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            await UpdateUserRoleAsync(discordUserId, role, "ユーザー権限を更新しました。");
        }

        [SlashCommand("remove-user", "ユーザーを無効化または削除する")]
        public async Task RemoveUser([Summary("discord-user-id")] string discordUserIdStr)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            var targetUser = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (targetUser == null)
            {
                await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                return;
            }

            if (!targetUser.IsActive)
            {
                await RespondAsync("対象ユーザーは既に無効化されています。", ephemeral: true);
                return;
            }

            targetUser.Deactivate();
            await _dbContext.SaveChangesAsync();
            await RespondAsync($"ユーザーを無効化しました: {targetUser.Name} ({targetUser.DiscordUserId})", ephemeral: true);
        }

        [SlashCommand("list-users", "登録済みユーザーを表示する")]
        public async Task ListUsers()
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var users = await _dbContext.Users.OrderBy(u => u.Id).ToListAsync();
                if (!users.Any())
                {
                    await RespondAsync("登録ユーザーは存在しません。", ephemeral: true);
                    return;
                }

                var lines = users.Select(u => $"ID:{u.Id} 名前:{u.Name} Role:{u.Role} 班ID:{(u.GroupId.HasValue ? u.GroupId.Value.ToString() : "未所属")} 有効:{u.IsActive}");
                await RespondAsync($"ユーザー一覧\n{string.Join("\n", lines)}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"ユーザー一覧取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("user-info", "ユーザーの所属・権限情報を表示する")]
        public async Task UserInfo([Summary("discord-user-id")] string discordUserIdStr)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }
            {
                try
                {
                    var caller = await GetCallerAsync();
                    if (!await EnsureAdminAsync(caller))
                    {
                        return;
                    }

                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);
                    if (user == null)
                    {
                        await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                        return;
                    }

                    var groupName = user.GroupId.HasValue
                        ? await _dbContext.Groups.Where(g => g.Id == user.GroupId.Value).Select(g => g.Name).FirstOrDefaultAsync()
                        : null;

                    var groupText = user.GroupId.HasValue
                        ? $"班ID:{user.GroupId.Value} 班名:{groupName ?? "不明"}"
                        : "班:未所属";

                    await RespondAsync($"ユーザー情報\nID:{user.Id}\n名前:{user.Name}\nDiscordUserId:{user.DiscordUserId}\nRole:{user.Role}\n{groupText}\n有効:{user.IsActive}", ephemeral: true);
                }
                catch (Exception ex)
                {
                    await RespondAsync($"ユーザー情報取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
                }
            }
        }

        [SlashCommand("grant-role", "ユーザーへ権限を付与する")]
        public async Task GrantRole(
            [Summary("discord-user-id")] string discordUserIdStr,
            [Summary("role")] AccountRole role)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            await UpdateUserRoleAsync(discordUserId, role, "権限を付与しました。");
        }

        [SlashCommand("revoke-role", "ユーザーから権限を解除する")]
        public async Task RevokeRole([Summary("discord-user-id")] string discordUserIdStr)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            await UpdateUserRoleAsync(discordUserId, AccountRole.GroupLeader, "権限を解除しました。", "ユーザーの権限を GroupLeader に戻しました。");
        }

        [SlashCommand("assign-group", "ユーザーを班へ所属させる")]
        public async Task AssignGroup(
            [Summary("discord-user-id")] string discordUserIdStr,
            [Summary("group-id")] int groupId)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);
                if (targetUser == null)
                {
                    await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                    return;
                }

                var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                {
                    await RespondAsync($"エラー: 指定された班が見つかりません: {groupId}", ephemeral: true);
                    return;
                }

                targetUser.ChangeGroupId(groupId);
                await _dbContext.SaveChangesAsync();
                await RespondAsync($"ユーザー {targetUser.Name} を班 {group.Name} に所属させました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"班所属設定中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("unassign-group", "ユーザーの班所属を解除する")]
        public async Task UnassignGroup([Summary("discord-user-id")] string discordUserIdStr)
        {
            if (!ulong.TryParse(discordUserIdStr, out var discordUserId))
            {
                await RespondAsync("エラー: 無効な Discord ユーザー ID が指定されました。", ephemeral: true);
                return;
            }

            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);
                if (targetUser == null)
                {
                    await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                    return;
                }

                if (!targetUser.GroupId.HasValue)
                {
                    await RespondAsync("対象ユーザーは既に未所属です。", ephemeral: true);
                    return;
                }

                targetUser.ChangeGroupId(null);
                await _dbContext.SaveChangesAsync();
                await RespondAsync($"ユーザー {targetUser.Name} の班所属を解除しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"班所属解除中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("group-members", "班ごとの所属メンバー一覧を表示する")]
        public async Task GroupMembers([Summary("group-id")] int groupId)
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

                // 管理者または会長は任意の班を参照可能、それ以外は自班のみ
                var isPrivileged = caller.Role == AccountRole.Admin || caller.Role == AccountRole.President;
                if (!isPrivileged && (!caller.GroupId.HasValue || caller.GroupId.Value != groupId))
                {
                    await RespondAsync("エラー: 指定班のメンバーを参照する権限がありません。", ephemeral: true);
                    return;
                }

                var members = await _dbContext.Users.Where(u => u.GroupId == groupId).OrderBy(u => u.Id).ToListAsync();
                if (!members.Any())
                {
                    await RespondAsync("指定班のメンバーは見つかりませんでした。", ephemeral: true);
                    return;
                }

                var lines = members.Select(u => $"ID:{u.Id} 名前:{u.Name} Role:{u.Role} 有効:{u.IsActive}");
                await RespondAsync($"班 {groupId} メンバー一覧\n{string.Join("\n", lines)}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"班メンバー取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        private async Task<User?> GetCallerAsync()
        {
            return await _userRepository.GetByDiscordUserIdAsync(Context.User.Id);
        }

        private async Task<bool> EnsureAdminAsync(User? caller)
        {
            if (caller == null)
            {
                await RespondAsync("エラー: Discord ユーザーが登録されていません。", ephemeral: true);
                return false;
            }

            if (caller.Role != AccountRole.Admin)
            {
                await RespondAsync("エラー: このコマンドは管理者のみ実行できます。", ephemeral: true);
                return false;
            }

            return true;
        }

        private async Task UpdateUserRoleAsync(ulong discordUserId, AccountRole role, string successMessage, string? differentMessage = null)
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);
                if (targetUser == null)
                {
                    await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                    return;
                }

                if (targetUser.Role == role && string.IsNullOrWhiteSpace(differentMessage))
                {
                    await RespondAsync("対象ユーザーは既に指定された権限です。", ephemeral: true);
                    return;
                }

                if (targetUser.Role == role && !string.IsNullOrWhiteSpace(differentMessage))
                {
                    await RespondAsync(differentMessage, ephemeral: true);
                    return;
                }

                targetUser.ChangeRole(role);
                await _dbContext.SaveChangesAsync();
                await RespondAsync($"{targetUser.Name} に対して {successMessage}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"権限更新中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}
