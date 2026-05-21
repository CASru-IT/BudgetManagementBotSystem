using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class BudgetModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("remaining-budget", "現在の残予算を確認する")]
        public async Task RemainingBudget() => await RespondAsync("未実装: 残予算");

        [SlashCommand("usage-history", "予算使用履歴を表示する")]
        public async Task UsageHistory() => await RespondAsync("未実装: 使用履歴");

        [SlashCommand("register-budget", "年度ごとの班予算を登録する")]
        public async Task RegisterBudget() => await RespondAsync("未実装: 予算登録");

        [SlashCommand("add-budget", "追加予算を付与する")]
        public async Task AddBudget() => await RespondAsync("未実装: 予算追加");

        [SlashCommand("change-budget", "登録済み予算を修正する")]
        public async Task ChangeBudget() => await RespondAsync("未実装: 予算変更");

        [SlashCommand("create-year", "新年度データを作成する")]
        public async Task CreateYear() => await RespondAsync("未実装: 年度作成");

        [SlashCommand("low-budget-warnings", "残予算が少ない班を表示する")]
        public async Task LowBudgetWarnings() => await RespondAsync("未実装: 超過警告一覧");

        [SlashCommand("budget-ranking", "班ごとの予算使用率ランキングを表示する")]
        public async Task BudgetRanking() => await RespondAsync("未実装: 予算ランキング");

        [SlashCommand("monthly-summary", "今月の支出状況を集計表示する")]
        public async Task MonthlySummary() => await RespondAsync("未実装: 今月集計");

        [SlashCommand("all-history", "全班の予算使用履歴を閲覧する")]
        public async Task AllHistory() => await RespondAsync("未実装: 全履歴");
    }
}
