using BudgetManagementBotSystem.Application.UseCases.Budget;
using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class BudgetCancellationEmbeds
{
    public static Embed Confirmation(BudgetCancellationPreview preview) => Build(preview, false);
    public static Embed Completed(BudgetCancellationPreview preview) => Build(preview, true);

    private static Embed Build(BudgetCancellationPreview p, bool completed)
    {
        var embed = new EmbedBuilder()
            .WithTitle(completed ? "予算追加を取り消しました" : "この予算追加を取り消しますか？")
            .WithColor(completed ? Color.LightGrey : Color.Gold)
            .AddField("履歴ID", p.TransactionId.ToString(), true)
            .AddField("班", $"{p.GroupName} (`{p.GroupId}`)", true)
            .AddField("会計年度", p.FiscalYear.ToString(), true)
            .AddField("元の取引日時（日本時間）", $"{p.TransactionDate.AddHours(9):yyyy-MM-dd HH:mm:ss}")
            .AddField("取消金額", $"{p.Amount:N2}円", true)
            .AddField("実残高", $"{p.BalanceBefore:N2}円 → {p.BalanceAfter:N2}円")
            .AddField("未承認申請合計", $"{p.PendingTotal:N2}円", true)
            .AddField("取消理由", p.Reason)
            .WithFooter(completed ? "元の追加履歴と、この取消の実行者・日時・理由を保存しました。" : "確認は10分間有効です。対象の班・日時・金額を確認してください。");
        if (p.IsLegacy && !completed)
            embed.WithDescription("この旧履歴は収入の由来が記録されていません。予算追加か、承認取消による返金かを確認し、誤った予算追加だけを選んでください。");
        return embed.Build();
    }

    public static MessageComponent Buttons(string token) => new ComponentBuilder()
        .WithButton("取り消す", $"budget:cancel:confirm:{token}", ButtonStyle.Danger)
        .WithButton("キャンセル", $"budget:cancel:abort:{token}", ButtonStyle.Secondary)
        .Build();
}
