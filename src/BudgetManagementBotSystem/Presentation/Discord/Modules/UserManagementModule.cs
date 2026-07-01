using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class UserManagementModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterUserUseCase _registerUserUseCase;
        private readonly UserQueryUseCase _userQuery;
        private readonly UserCommandUseCase _userCommand;
        private readonly ILogger<UserManagementModule> _logger;

        public UserManagementModule(
            RegisterUserUseCase registerUserUseCase,
            UserQueryUseCase userQuery,
            UserCommandUseCase userCommand,
            ILogger<UserManagementModule> logger)
        {
            _registerUserUseCase = registerUserUseCase;
            _userQuery = userQuery;
            _userCommand = userCommand;
            _logger = logger;
        }

        [SlashCommand("register-user", "システム利用ユーザーを登録します")]
        public async Task RegisterUser(
            [Summary("user")] IUser targetUser,
            [Summary("role")] AccountRole role)
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var discordUserName =
                    (targetUser as IGuildUser)?.Nickname
                    ?? targetUser.GlobalName
                    ?? targetUser.Username;

                var existingUser = await _userQuery.GetByDiscordIdAsync(targetUser.Id);
                if (existingUser != null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("このDiscordユーザーは既に登録されています。"), ephemeral: true);
                    return;
                }

                await _registerUserUseCase.ExecuteAsync(discordUserName, targetUser.Id, role);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("ユーザーを登録しました", $"{discordUserName} を `{role}` として登録しました。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register user. DiscordUserId: {DiscordUserId}, TargetDiscordUserId: {TargetDiscordUserId}, Role: {Role}", Context.User.Id, targetUser.Id, role);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("ユーザー登録を完了できません", "入力内容を確認して再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("set-user-name", "ユーザーの表示名を変更します")]
        public async Task SetUserName(
            [Summary("user-id")] int userId,
            [Summary("name")] string name)
        {
            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.UpdateUserNameByIdAsync(userId, name);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("ユーザー名を更新しました", $"ユーザーID `{userId}` の表示名を更新しました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
            }
        }

        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定します")]
        public async Task SetUserRole(
            [Summary("user-id")] int userId,
            [Summary("role")] AccountRole role)
        {
            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.UpdateUserRoleByIdAsync(userId, role);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("ユーザー権限を更新しました", $"ユーザーID `{userId}` のRoleを `{role}` に更新しました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
            }
        }

        [SlashCommand("remove-user", "ユーザーを無効化します")]
        public async Task RemoveUser([Summary("user-id")] int userId)
        {
            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.DeactivateUserByIdAsync(userId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("ユーザーを無効化しました", $"ユーザーID `{userId}` を無効化しました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
            }
        }

        [SlashCommand("activate-user", "ユーザーを有効化します")]
        public async Task ActivateUser([Summary("user-id")] int userId)
        {
            var caller = await GetCallerAsync();
            if (!await EnsureAdminAsync(caller))
            {
                return;
            }

            try
            {
                await _userCommand.ActivateUserByIdAsync(userId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("ユーザーを有効化しました", $"ユーザーID `{userId}` を有効化しました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
            }
        }

        [SlashCommand("list-users", "登録済みユーザーを表示します")]
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
                await RespondAsync(embed: DiscordEmbedFactory.BuildUserListEmbed(users), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list users. DiscordUserId: {DiscordUserId}", Context.User.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("ユーザー一覧を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("user-info", "ユーザーの所属や権限情報を表示します")]
        public async Task UserInfo([Summary("user-id")] int userId)
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var user = await _userQuery.GetByIdAsync(userId);
                if (user == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
                    return;
                }

                await RespondAsync(embed: DiscordEmbedFactory.BuildUserInfoEmbed(user), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user info. DiscordUserId: {DiscordUserId}, UserId: {UserId}", Context.User.Id, userId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("ユーザー情報を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("assign-group", "ユーザーを班へ所属させます")]
        public async Task AssignGroup(
            [Summary("user-id")] int userId,
            [Summary("group-id")] int groupId)
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                var groupName = await _userCommand.AssignGroupByUserIdAsync(userId, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("班所属を設定しました", $"ユーザーID `{userId}` を班 `{groupName}` (`{groupId}`) に所属させました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("ユーザーIDまたは班IDを確認してください。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign group. DiscordUserId: {DiscordUserId}, UserId: {UserId}, GroupId: {GroupId}", Context.User.Id, userId, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班所属を設定できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("unassign-group", "ユーザーの班所属を解除します")]
        public async Task UnassignGroup([Summary("user-id")] int userId)
        {
            try
            {
                var caller = await GetCallerAsync();
                if (!await EnsureAdminAsync(caller))
                {
                    return;
                }

                await _userCommand.UnassignGroupByUserIdAsync(userId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("班所属を解除しました", $"ユーザーID `{userId}` の班所属を解除しました。"), ephemeral: true);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("ユーザー", userId.ToString()), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unassign group. DiscordUserId: {DiscordUserId}, UserId: {UserId}", Context.User.Id, userId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班所属を解除できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("group-members", "班ごとの所属メンバー一覧を表示します")]
        public async Task GroupMembers([Summary("group-id")] int groupId)
        {
            try
            {
                var caller = await _userQuery.GetByDiscordIdAsync(Context.User.Id);
                if (caller == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。"), ephemeral: true);
                    return;
                }

                var isPrivileged = caller.Role == AccountRole.Admin || caller.Role == AccountRole.President;
                if (!isPrivileged && (!caller.GroupId.HasValue || caller.GroupId.Value != groupId))
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("指定班のメンバーを参照する権限がありません。"), ephemeral: true);
                    return;
                }

                var members = await _userQuery.GetMembersByGroupIdAsync(groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildGroupMembersEmbed(groupId, members), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get group members. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}", Context.User.Id, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("班メンバーを取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        private async Task<User?> GetCallerAsync()
        {
            return await _userQuery.GetByDiscordIdAsync(Context.User.Id);
        }

        private async Task<bool> EnsureAdminAsync(User? caller)
        {
            if (caller == null)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。"), ephemeral: true);
                return false;
            }

            if (caller.Role != AccountRole.Admin)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("このコマンドは管理者のみ実行できます。"), ephemeral: true);
                return false;
            }

            return true;
        }
    }
}
