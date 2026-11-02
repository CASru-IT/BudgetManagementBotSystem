using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.Repository;
using System.Globalization;

namespace BudgetManagementBotSystem.Application.UseCases.Budget;

public sealed record BudgetCancellationPreview(
    int TransactionId, int GroupId, string GroupName, decimal Amount, int FiscalYear,
    DateTime TransactionDate, decimal BalanceBefore, decimal BalanceAfter,
    decimal PendingTotal, Guid Revision, bool IsLegacy, string Reason);

public sealed record BudgetAdditionCandidate(int TransactionId, string GroupName, decimal Amount,
    int FiscalYear, DateTime TransactionDate, bool IsLegacy);

public class CancelBudgetAdditionUseCase(
    IGroupRepository groups, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<BudgetAdditionCandidate>> GetCandidatesAsync(ulong discordUserId, string search)
    {
        await AuthorizeAsync(discordUserId);
        var input = search.Trim();
        var candidates = new List<BudgetAdditionCandidate>();
        foreach (var group in await groups.GetAllAsync() ?? [])
        {
            foreach (var transaction in group.BudgetTransactions)
            {
                if (!Matches(group, transaction, input)) continue;
                try { group.ValidateBudgetCancellation(transaction.Id); }
                catch (BudgetCancellationRejectedException) { continue; }
                candidates.Add(new(transaction.Id, group.Name, transaction.Amount.Value, transaction.FiscalYear.Year,
                    transaction.TransactionDate, transaction.Kind == BudgetTransactionKind.Legacy));
            }
        }
        return candidates.OrderByDescending(c => c.TransactionId.ToString(CultureInfo.InvariantCulture) == input)
            .ThenByDescending(c => c.TransactionDate).ThenByDescending(c => c.TransactionId).Take(25).ToArray();
    }

    private static bool Matches(Group group, BudgetTransaction transaction, string input)
        => input.Length == 0
            || transaction.Id.ToString(CultureInfo.InvariantCulture).StartsWith(input, StringComparison.Ordinal)
            || group.Name.Contains(input, StringComparison.OrdinalIgnoreCase)
            || transaction.Amount.Value.ToString("F2", CultureInfo.InvariantCulture).Contains(input, StringComparison.Ordinal)
            || transaction.Amount.Value.ToString("N2", CultureInfo.InvariantCulture).Contains(input, StringComparison.Ordinal)
            || transaction.TransactionDate.AddHours(9).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).Contains(input, StringComparison.Ordinal)
            || transaction.FiscalYear.Year.ToString(CultureInfo.InvariantCulture).Contains(input, StringComparison.Ordinal);

    public async Task<BudgetCancellationPreview> PreviewAsync(ulong discordUserId, int transactionId, string reason)
    {
        await AuthorizeAsync(discordUserId);
        return BuildPreview(await FindGroupAsync(transactionId), transactionId, reason);
    }

    public async Task<BudgetCancellationPreview> ExecuteAsync(ulong discordUserId, int transactionId, string reason, Guid expectedRevision)
    {
        var actor = await AuthorizeAsync(discordUserId);
        var group = await FindGroupAsync(transactionId);
        var preview = BuildPreview(group, transactionId, reason);
        if (group.BudgetRevision != expectedRevision)
            throw new BudgetCancellationRejectedException("確認後に予算または申請が更新されました。もう一度 /cancel-budget で最新の残高を確認してください。");
        group.CancelBudgetAddition(transactionId, actor.Id, preview.Reason);
        await unitOfWork.SaveChangesAsync();
        return preview;
    }

    private async Task<User> AuthorizeAsync(ulong discordUserId)
    {
        var user = await users.GetByDiscordUserIdAsync(discordUserId);
        if (user == null || !user.IsActive || user.Role is not (AccountRole.Admin or AccountRole.President or AccountRole.Accountant))
            throw new UnauthorizedAccessException("予算取消には有効な管理者・会長・会計担当の権限が必要です。");
        return user;
    }

    private async Task<Group> FindGroupAsync(int transactionId)
    {
        var all = await groups.GetAllAsync();
        return all?.SingleOrDefault(g => g.BudgetTransactions.Any(t => t.Id == transactionId))
            ?? throw new BudgetCancellationValidationException("指定された予算履歴が見つかりません。/budget-history で履歴IDを確認してください。");
    }

    private static BudgetCancellationPreview BuildPreview(Group group, int transactionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new BudgetCancellationValidationException("取消理由は1〜500文字で入力してください。", nameof(reason));
        var original = group.ValidateBudgetCancellation(transactionId);
        var before = group.GetTotalBudgetForFiscalYear(original.FiscalYear);
        var pending = group.Requests.Where(r => r.FiscalYear == original.FiscalYear && r.GetCurrentStatus() == RequestStatus.Pending)
            .Sum(r => r.Amount.Value);
        return new(original.Id, group.Id, group.Name, original.Amount.Value, original.FiscalYear.Year,
            original.TransactionDate, before, before - original.Amount.Value, pending,
            group.BudgetRevision, original.Kind == BudgetTransactionKind.Legacy, reason.Trim());
    }
}
