using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class UserManagementModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("set-user-role", "ユーザーの権限やロールを設定する")]
        public async Task SetUserRole() => await RespondAsync("未実装: ユーザー権限設定");

        [SlashCommand("register-user", "システム利用ユーザーを登録する")]
        public async Task RegisterUser() => await RespondAsync("未実装: ユーザー登録");

        [SlashCommand("remove-user", "ユーザーを無効化または削除する")]
        public async Task RemoveUser() => await RespondAsync("未実装: ユーザー削除");

        [SlashCommand("list-users", "登録済みユーザーを表示する")]
        public async Task ListUsers() => await RespondAsync("未実装: ユーザー一覧表示");

        [SlashCommand("user-info", "ユーザーの所属・権限情報を表示する")]
        public async Task UserInfo(string user) => await RespondAsync($"未実装: {user}のユーザー情報表示");

        [SlashCommand("grant-role", "ユーザーへ権限を付与する")]
        public async Task GrantRole() => await RespondAsync("未実装: 権限付与");

        [SlashCommand("revoke-role", "ユーザーから権限を解除する")]
        public async Task RevokeRole() => await RespondAsync("未実装: 権限解除");

        [SlashCommand("assign-group", "ユーザーを班へ所属させる")]
        public async Task AssignGroup() => await RespondAsync("未実装: ユーザーを班に所属");

        [SlashCommand("unassign-group", "ユーザーの班所属を解除する")]
        public async Task UnassignGroup() => await RespondAsync("未実装: ユーザーの班所属解除");

        [SlashCommand("group-members", "班ごとの所属メンバー一覧を表示する")]
        public async Task GroupMembers() => await RespondAsync("未実装: 班メンバー一覧表示");
    }
}
