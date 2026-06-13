using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using Discord;
using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class DmDiagnosticsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IUserRepository _userRepository;
        private readonly DiscordBotService _discordBotService;

        public DmDiagnosticsModule(IUserRepository userRepository, DiscordBotService discordBotService)
        {
            _userRepository = userRepository;
            _discordBotService = discordBotService;
        }

        [SlashCommand("test-dm", "指定ユーザーへテスト DM を送信し、送信可否と失敗理由を確認します")]
        public async Task TestDm(
            [Summary("user-id")] int userId,
            [Summary("message")] string? message = null)
        {
            var caller = await _userRepository.GetByDiscordUserIdAsync(Context.User.Id);
            if (caller == null)
            {
                await RespondAsync(embed: BuildAuthorizationErrorEmbed("Discord ユーザーが登録されていません。"), ephemeral: true);
                return;
            }

            if (caller.Role != AccountRole.Admin)
            {
                await RespondAsync(embed: BuildAuthorizationErrorEmbed("このコマンドは管理者のみ実行できます。"), ephemeral: true);
                return;
            }

            var targetUser = await _userRepository.GetByIdAsync(userId);
            if (targetUser == null)
            {
                await RespondAsync(embed: BuildAuthorizationErrorEmbed($"User ID {userId} のユーザーが見つかりません。"), ephemeral: true);
                return;
            }

            var dmEmbed = BuildTestDmEmbed(Context.User, targetUser.Id, targetUser.Name, targetUser.DiscordUserId, message);
            var result = await _discordBotService.TestDirectMessageAsync(targetUser.DiscordUserId, dmEmbed);
            var resultEmbed = BuildTestDmResultEmbed(targetUser.Id, targetUser.Name, targetUser.DiscordUserId, result);

            await RespondAsync(embed: resultEmbed, ephemeral: true);
        }

        private static string GetDisplayName(IUser user)
        {
            return (user as IGuildUser)?.Nickname
                ?? user.GlobalName
                ?? user.Username;
        }

        private static Embed BuildTestDmEmbed(IUser sender, int userId, string targetName, ulong targetDiscordUserId, string? message)
        {
            var senderName = GetDisplayName(sender);
            var description = string.IsNullOrWhiteSpace(message)
                ? "これは Bot からの DM 送信確認メッセージです。"
                : message;

            return new EmbedBuilder()
                .WithTitle("DM 送信テスト")
                .WithColor(Color.Blue)
                .WithDescription(description)
                .AddField("対象ユーザー", $"{targetName} (User ID: {userId}, Discord ID: {targetDiscordUserId})", false)
                .AddField("実行者", $"{senderName} ({sender.Id})", false)
                .WithFooter("このメッセージは管理者のテストコマンドにより送信されました。")
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildTestDmResultEmbed(int userId, string targetName, ulong targetDiscordUserId, DirectMessageSendResult result)
        {
            var builder = new EmbedBuilder()
                .WithTitle(result.IsSuccess ? "DM 送信テスト成功" : "DM 送信テスト失敗")
                .WithColor(result.IsSuccess ? Color.Green : Color.Red)
                .WithDescription(result.Summary)
                .AddField("対象ユーザー", $"{targetName} (User ID: {userId}, Discord ID: {targetDiscordUserId})", false)
                .AddField("結果", result.IsSuccess ? "成功" : "失敗", true)
                .AddField("詳細", result.Detail, false)
                .WithCurrentTimestamp();

            if (!string.IsNullOrWhiteSpace(result.ExceptionType))
            {
                builder.AddField("例外種別", result.ExceptionType, false);
            }

            if (!string.IsNullOrWhiteSpace(result.HttpCode))
            {
                builder.AddField("HTTP ステータス", result.HttpCode, true);
            }

            if (!string.IsNullOrWhiteSpace(result.DiscordCode))
            {
                builder.AddField("Discord エラーコード", result.DiscordCode, true);
            }

            if (!string.IsNullOrWhiteSpace(result.DiscordReason))
            {
                builder.AddField("Discord 理由", result.DiscordReason, false);
            }

            return builder.Build();
        }

        private static Embed BuildAuthorizationErrorEmbed(string reason)
        {
            return new EmbedBuilder()
                .WithTitle("DM 送信テストを実行できません")
                .WithColor(Color.Red)
                .WithDescription(reason)
                .Build();
        }
    }
}
