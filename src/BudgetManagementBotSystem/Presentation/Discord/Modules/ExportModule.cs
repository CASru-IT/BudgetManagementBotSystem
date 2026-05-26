using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Application.UseCases.Export;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class ExportModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ExportUseCase _exportUseCase;

        public ExportModule(ExportUseCase exportUseCase)
        {
            _exportUseCase = exportUseCase;
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
                        csv = await _exportUseCase.ExportUsersCsvAsync();
                        break;
                    case "transactions":
                    case "tx":
                        csv = await _exportUseCase.ExportTransactionsCsvAsync();
                        break;
                    case "requests":
                    case "reqs":
                        csv = await _exportUseCase.ExportRequestsCsvAsync(Context.User.Id);
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

                // Save CSV via IFileStorage and return saved path to caller
                await RespondAsync($"CSV を生成し保存しました。保存先パス: {csv}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"CSV 出力中にエラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        // ExportUseCase handles CSV generation now.
    }
}
