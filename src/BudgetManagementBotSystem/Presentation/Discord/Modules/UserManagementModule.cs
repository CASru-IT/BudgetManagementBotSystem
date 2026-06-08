using Discord;
using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class UserManagementModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterUserUseCase _registerUserUseCase;
        private readonly Domain.Repository.IUserRepository _userRepository;
        private readonly UserQueryUseCase _userQuery;
        private readonly UserCommandUseCase _userCommand;

        public UserManagementModule(RegisterUserUseCase registerUserUseCase, Domain.Repository.IUserRepository userRepository, BudgetManagementBotSystem.Application.UseCases.UserManagement.UserQueryUseCase userQuery, BudgetManagementBotSystem.Application.UseCases.UserManagement.UserCommandUseCase userCommand)
        {
            _registerUserUseCase = registerUserUseCase;
            _userRepository = userRepository;
            _userQuery = userQuery;
            _userCommand = userCommand;
        }

        [SlashCommand("register-user", "システム利用ユーザーを登録する")]
        public async Task RegisterUser(
            [Summary("user")] IUser targetUser,
            [Summary("role")] AccountRole role)
        {
            var discordUserId = targetUser.Id;
            var discordUserName = targetUser.Username;

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

            await _registerUserUseCase.ExecuteAsync(discordUserName, discordUserId, role);
            await RespondAsync($"ユーザーを登録しました: {discordUserName} ({role})", ephemeral: true);
        }

        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定する")]
        public async Task SetUserRole(
            [Summary("user")] IUser targetUser,
            [Summary("role")] AccountRole role)
        {
            var discordUserId = targetUser.Id;

            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.UpdateUserRoleByDiscordIdAsync(discordUserId, role);
                await RespondAsync("ユーザー権限を更新しました。", ephemeral: true);
            }
            catch (ArgumentException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("remove-user", "ユーザーを無効化または削除する")]
        public async Task RemoveUser([Summary("user")] IUser targetUser)
        {
            var discordUserId = targetUser.Id;

            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.DeactivateUserByDiscordIdAsync(discordUserId);
                await RespondAsync($"ユーザーを無効化しました: {discordUserId}", ephemeral: true);
            }
            catch (ArgumentException ex)
            {
                await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
            }
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

                var users = await _userQuery.ListUsersAsync();
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
        public async Task UserInfo([Summary("user")] IUser targetUser)
        {
            var discordUserId = targetUser.Id;
            {
                try
                {
                    var caller = await GetCallerAsync();
                    if (!await EnsureAdminAsync(caller))
                    {
                        return;
                    }

                    var user = await _userQuery.GetByDiscordIdAsync(discordUserId);
                    if (user == null)
                    {
                        await RespondAsync("エラー: 指定されたユーザーが見つかりません。", ephemeral: true);
                        return;
                    }

                    var groupText = user.GroupId.HasValue ? $"班ID:{user.GroupId.Value}" : "班:未所属";

                    await RespondAsync($"ユーザー情報\nID:{user.Id}\n名前:{user.Name}\nDiscordUserId:{user.DiscordUserId}\nRole:{user.Role}\n{groupText}\n有効:{user.IsActive}", ephemeral: true);
                }
                catch (Exception ex)
                {
                    await RespondAsync($"ユーザー情報取得中にエラーが発生しました: {ex.Message}", ephemeral: true);
                }
            }
        }

        [SlashCommand("assign-group", "ユーザーを班へ所属させる")]
        public async Task AssignGroup(
            [Summary("user")] IUser targetUser,
            [Summary("group-id")] int groupId)
        {
            var discordUserId = targetUser.Id;
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                try
                {
                    var groupName = await _userCommand.AssignGroupByDiscordIdAsync(discordUserId, groupId);
                    await RespondAsync($"ユーザーを班 {groupName}（ID: {groupId}）に所属させました。", ephemeral: true);
                }
                catch (ArgumentException ex)
                {
                    await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await RespondAsync($"班所属設定中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("unassign-group", "ユーザーの班所属を解除する")]
        public async Task UnassignGroup([Summary("user")] IUser targetUser)
        {
            var discordUserId = targetUser.Id;

            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                try
                {
                    await _userCommand.UnassignGroupByDiscordIdAsync(discordUserId);
                    await RespondAsync($"ユーザーの班所属を解除しました。", ephemeral: true);
                }
                catch (ArgumentException ex)
                {
                    await RespondAsync($"エラー: {ex.Message}", ephemeral: true);
                }
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

                var isPrivileged = caller.Role == AccountRole.Admin || caller.Role == AccountRole.President;
                if (!isPrivileged && (!caller.GroupId.HasValue || caller.GroupId.Value != groupId))
                {
                    await RespondAsync("エラー: 指定班のメンバーを参照する権限がありません。", ephemeral: true);
                    return;
                }

                var members = await _userQuery.GetMembersByGroupIdAsync(groupId);
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
    }
}
