using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Repository;
using System.Linq;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SubmitBudgetRequestUseCase _submitBudgetRequestUseCase;
        private readonly IUserRepository _userRepository;

        public RequestModule(SubmitBudgetRequestUseCase submitBudgetRequestUseCase, IUserRepository userRepository)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _userRepository = userRepository;
        }

        [SlashCommand("officer-request", "役員会用の予算申請を行う")]
        public async Task OfficerRequest() => await RespondAsync("未実装: 役員会申請");

        [SlashCommand("create-request", "予算使用申請を作成する")]
        public async Task CreateRequest(
            [Summary("班ID")] int groupId,
            [Summary("金額（例: 1234.56）")] double amount,
            [Summary("用途説明")] string description)
        {
            try
            {
                var discordUserId = Context.User.Id;
                var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
                if (user == null)
                {
                    await RespondAsync("エラー: Discord ユーザーがシステムに登録されていません。管理者に登録を依頼してください。", ephemeral: true);
                    return;
                }

                decimal amountDec = Convert.ToDecimal(amount);

                await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, groupId, amountDec, description, Enumerable.Empty<string>());

                await RespondAsync($"申請を作成しました: 班 {groupId} 金額 {amountDec:C}");
            }
            catch (ArgumentNullException ex)
            {
                await RespondAsync($"入力エラー: {ex.ParamName} - {ex.Message}", ephemeral: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                await RespondAsync($"入力エラー: {ex.Message}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"予期せぬエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

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
