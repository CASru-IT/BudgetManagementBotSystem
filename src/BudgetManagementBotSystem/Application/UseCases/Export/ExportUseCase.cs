using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Repository;
using System.Text;

namespace BudgetManagementBotSystem.Application.UseCases.Export;

public class ExportUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly RequestWorkflow.RequestListUseCase _requestListUseCase;
    private readonly Budget.BudgetQueryUseCase _budgetQueryUseCase;
    private readonly RequestWorkflow.RequestDetailUseCase _requestDetailUseCase;
    private readonly IFileStorage _fileStorage;

    public ExportUseCase(IUserRepository userRepository, RequestWorkflow.RequestListUseCase requestListUseCase, Budget.BudgetQueryUseCase budgetQueryUseCase, RequestWorkflow.RequestDetailUseCase requestDetailUseCase, IFileStorage fileStorage)
    {
        _userRepository = userRepository;
        _requestListUseCase = requestListUseCase;
        _budgetQueryUseCase = budgetQueryUseCase;
        _requestDetailUseCase = requestDetailUseCase;
        _fileStorage = fileStorage;
    }

    private static string EscapeCsv(string? s)
    {
        if (s == null) return string.Empty;
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return '"' + s + '"';
        }
        return s;
    }

    private async Task<string> SaveCsvAsync(string baseName, string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return string.Empty;
        var fileName = $"{baseName}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _fileStorage.SaveFileAsync(fileName, ms);
    }

    public async Task<string> ExportUsersCsvAsync()
    {
        var users = await _userRepository.GetAllAsync();
        if (users == null || !users.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,DiscordUserId,Role,GroupId,IsActive");
        foreach (var u in users.OrderBy(u => u.Id))
        {
            sb.AppendLine(string.Join(",",
                u.Id.ToString(),
                EscapeCsv(u.Name),
                u.DiscordUserId.ToString(),
                EscapeCsv(u.Role.ToString()),
                u.GroupId.HasValue ? u.GroupId.Value.ToString() : string.Empty,
                u.IsActive ? "true" : "false"
            ));
        }

        var csv = sb.ToString();
        return await SaveCsvAsync("export-users", csv);
    }

    public async Task<string> ExportTransactionsCsvAsync(int take = 500)
    {
        var txs = await _budgetQueryUseCase.GetAllHistoryAsync(take);
        if (txs == null || !txs.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("GroupName,IsIncome,Amount,TransactionDate,FiscalYear");
        foreach (var t in txs)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(t.GroupName),
                t.IsIncome ? "true" : "false",
                t.Amount.ToString("F2"),
                t.TransactionDate.ToString("o"),
                t.FiscalYear.ToString()
            ));
        }

        var csv = sb.ToString();
        return await SaveCsvAsync("export-transactions", csv);
    }

    public async Task<string> ExportRequestsCsvAsync(ulong callerDiscordId, int take = 500)
    {
        var page = await _requestListUseCase.ExecuteAsync(callerDiscordId, null, 1, take, null);
        if (page == null || page.Items == null || !page.Items.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Id,UserId,GroupId,Amount,Status,RequestDate,Description");

        foreach (var r in page.Items)
        {
            var detail = await _requestDetailUseCase.GetByIdAsync(r.Id);
            var userId = detail.request?.UserId.ToString() ?? string.Empty;
            var status = detail.request?.StatusHistory.LastOrDefault()?.ChangedStatus.ToString() ?? string.Empty;

            sb.AppendLine(string.Join(",",
                r.Id.ToString(),
                userId,
                r.GroupId.ToString(),
                r.Amount.ToString("F2"),
                EscapeCsv(status),
                r.RequestDate.ToString("o"),
                EscapeCsv(r.Description)
            ));
        }

        var csv = sb.ToString();
        return await SaveCsvAsync("export-requests", csv);
    }
}
