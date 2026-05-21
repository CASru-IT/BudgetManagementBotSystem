using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("officer-request", "役員会用の予算申請を行う")]
        public async Task OfficerRequest() => await RespondAsync("未実装: 役員会申請");

        [SlashCommand("create-request", "予算使用申請を作成する")]
        public async Task CreateRequest() => await RespondAsync("未実装: 申請作成");

        [SlashCommand("list-requests", "自分の班または役員会の申請一覧を表示する")]
        public async Task ListRequests() => await RespondAsync("未実装: 申請一覧");

        [SlashCommand("request-detail", "指定した申請の詳細を表示する")]
        public async Task RequestDetail([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請詳細 {requestId}");

        [SlashCommand("cancel-request", "確認待ち状態の申請を取り消す")]
        public async Task CancelRequest([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請取消 {requestId}");

        [SlashCommand("reapply", "過去の申請内容をコピーして再申請する")]
        public async Task Reapply([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 再申請 {requestId}");

        [SlashCommand("expired-requests", "長期間未処理の申請を表示する")]
        public async Task ExpiredRequests() => await RespondAsync("未実装: 期限切れ申請");
    }
}
