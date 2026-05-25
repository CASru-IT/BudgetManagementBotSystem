using Discord.Interactions;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ExportModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BudgetManagementDbContext _dbContext;

        public ExportModule(BudgetManagementDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [SlashCommand("export-csv", "履歴や予算情報をCSV形式で出力する")]
        public async Task ExportCsv([Summary("target")] string target)
        {
            try
            {
                target = (target ?? string.Empty).Trim().ToLowerInvariant();
                string csv;
                switch (target)
                {
                    case "users":
                        csv = await ExportUsersAsync();
                        break;
                    case "transactions":
                    case "tx":
                        csv = await ExportTransactionsAsync();
                        break;
                    case "requests":
                    case "reqs":
                        csv = await ExportRequestsAsync();
                        break;
                    default:
                        await RespondAsync("対象が不正です。`users` / `transactions` / `requests` を指定してください。", ephemeral: true);
                        return;
                }

                if (string.IsNullOrWhiteSpace(csv))
                {
                    await RespondAsync("エクスポートするデータが見つかりませんでした。", ephemeral: true);
                    return;
                }

                // Discord への大きなファイル送信は避けるため、CSV をメッセージとして返す（小規模データ想定）。
                // 大量データはファイル保存＋ダウンロード提供へ拡張してください。
                await RespondAsync($"CSV出力 ({target})\n```csv\n{csv}\n```", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"CSV 出力中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return string.Empty;
            if (s.Contains('"')) s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            {
                return '"' + s + '"';
            }
            return s;
        }

        private async Task<string> ExportUsersAsync()
        {
            var users = await _dbContext.Users.OrderBy(u => u.Id).ToListAsync();
            if (!users.Any()) return string.Empty;

            var lines = new List<string> { "Id,Name,DiscordUserId,Role,GroupId,IsActive" };
            lines.AddRange(users.Select(u => string.Join(",",
                u.Id.ToString(),
                EscapeCsv(u.Name),
                u.DiscordUserId.ToString(),
                EscapeCsv(u.Role.ToString()),
                u.GroupId.HasValue ? u.GroupId.Value.ToString() : string.Empty,
                u.IsActive ? "true" : "false"
            )));

            return string.Join("\n", lines);
        }

        private async Task<string> ExportTransactionsAsync()
        {
            var txs = await _dbContext.BudgetTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(500)
                .ToListAsync();

            if (!txs.Any()) return string.Empty;

            // collect group ids referenced by shadow property
            var groupIds = txs.Select(t => _dbContext.Entry(t).Property<int>("GroupId").CurrentValue).Distinct().ToList();
            var groups = await _dbContext.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
            var groupMap = groups.ToDictionary(g => g.Id, g => g.Name);

            var lines = new List<string> { "GroupName,IsIncome,Amount,TransactionDate,FiscalYear" };
            foreach (var t in txs)
            {
                var gid = _dbContext.Entry(t).Property<int>("GroupId").CurrentValue;
                var gname = groupMap.TryGetValue(gid, out var n) ? n : string.Empty;
                lines.Add(string.Join(",", EscapeCsv(gname), t.IsIncome ? "true" : "false", t.Amount.Value.ToString("F2"), t.TransactionDate.ToString("o"), t.FiscalYear.Year.ToString()));
            }

            return string.Join("\n", lines);
        }

        private async Task<string> ExportRequestsAsync()
        {
            var reqs = await _dbContext.BudgetRequests
                .Include(r => r.StatusHistory)
                .OrderByDescending(r => r.RequestDate)
                .Take(500)
                .ToListAsync();

            if (!reqs.Any()) return string.Empty;

            var lines = new List<string> { "Id,UserId,GroupId,Amount,Status,RequestDate,Description" };
            lines.AddRange(reqs.Select(r => string.Join(",",
                r.Id.ToString(),
                r.UserId.ToString(),
                // GroupId is shadow property
                (_dbContext.Entry(r).Property<int?>("GroupId").CurrentValue.HasValue ? _dbContext.Entry(r).Property<int?>("GroupId").CurrentValue.Value.ToString() : string.Empty),
                r.Amount.Value.ToString("F2"),
                r.StatusHistory.Last().ChangedStatus.ToString(),
                r.RequestDate.ToString("o"),
                EscapeCsv(r.Description)
            )));

            return string.Join("\n", lines);
        }
    }
}
