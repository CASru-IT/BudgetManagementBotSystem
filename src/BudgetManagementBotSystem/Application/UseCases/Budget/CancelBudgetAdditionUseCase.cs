using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.Budget;

public sealed record BudgetCancellationPreview(
    int TransactionId, int GroupId, string GroupName, decimal Amount, int FiscalYear,
    DateTime TransactionDate, decimal BalanceBefore, decimal BalanceAfter,
    decimal PendingTotal, Guid Revision, bool IsLegacy, string Reason);

public class CancelBudgetAdditionUseCase(
    IGroupRepository groups, IUserRepository users, IUnitOfWork unitOfWork)
{
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
            throw new BudgetCancellationRejectedException("確認後に予算または申請が更新されました。もう一度 /予算取消 で最新の残高を確認してください。");
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
            ?? throw new BudgetCancellationValidationException("指定された予算履歴が見つかりません。/予算履歴 で履歴IDを確認してください。");
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
