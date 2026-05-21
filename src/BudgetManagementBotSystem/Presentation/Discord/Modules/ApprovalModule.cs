using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ApprovalModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("pending-list", "未承認の申請一覧を表示する")]
        public async Task PendingList() => await RespondAsync("未実装: 確認待ち一覧");

        [SlashCommand("approve", "指定した申請を承認する")]
        public async Task Approve([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請承認 {requestId}");

        [SlashCommand("reject", "指定した申請を却下する")]
        public async Task Reject([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 申請却下 {requestId}");

        [SlashCommand("revoke-approval", "承認済み申請の承認を取り消す")]
        public async Task RevokeApproval([Summary("申請ID")] string requestId) => await RespondAsync($"未実装: 承認取消 {requestId}");

        [SlashCommand("finance-dashboard", "全班の予算・申請状況を一覧表示する")]
        public async Task FinanceDashboard() => await RespondAsync("未実装: 会計ダッシュボード");
    }
}
