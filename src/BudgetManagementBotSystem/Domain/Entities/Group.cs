using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;

namespace BudgetManagementBotSystem.Domain.Entities;

public class Group
{
    private readonly List<BudgetTransaction> _budgetTransactions = new List<BudgetTransaction>();
    private readonly List<BudgetRequest> _requests = new List<BudgetRequest>();

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid BudgetRevision { get; private set; }
    public IReadOnlyCollection<BudgetTransaction> BudgetTransactions => _budgetTransactions;
    public IReadOnlyCollection<BudgetRequest> Requests => _requests;

    public Group(string name)
    {
        Name = name;
    }

    public void AddBudgetTransaction(BudgetTransaction transaction)
    {
        if (GetTotalBudgetForFiscalYear(transaction.FiscalYear) + (transaction.IsIncome ? transaction.Amount.Value : -transaction.Amount.Value) < 0)
        {
            throw new InvalidOperationException("この取引を追加すると、予算がマイナスになります。");
        }
        _budgetTransactions.Add(transaction);
        BudgetRevision = Guid.NewGuid();
    }

    public BudgetTransaction ValidateBudgetCancellation(int transactionId)
    {
        var original = _budgetTransactions.SingleOrDefault(t => t.Id == transactionId)
            ?? throw new BudgetCancellationValidationException("指定された予算履歴が見つかりません。", nameof(transactionId));
        if (!original.IsIncome || original.Amount.Value <= 0 ||
            original.Kind is not (BudgetTransactionKind.Legacy or BudgetTransactionKind.BudgetAddition))
            throw new BudgetCancellationRejectedException("この履歴は予算追加ではないため取り消せません。");
        if (_budgetTransactions.Any(t => t.ReversesTransactionId == transactionId))
            throw new BudgetCancellationRejectedException("この予算追加はすでに取り消されています。");
        var balanceAfter = GetTotalBudgetForFiscalYear(original.FiscalYear) - original.Amount.Value;
        var pending = _requests.Where(r => r.FiscalYear == original.FiscalYear && r.GetCurrentStatus() == RequestStatus.Pending)
            .Sum(r => r.Amount.Value);
        if (balanceAfter < 0 || balanceAfter - pending < 0)
            throw new BudgetCancellationRejectedException("取消後の予算が使用済み額または未承認申請を含む必要額を下回ります。申請・支出を確認してください。");
        return original;
    }

    public BudgetTransaction CancelBudgetAddition(int transactionId, int actorId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new BudgetCancellationValidationException("取消理由は1〜500文字で入力してください。", nameof(reason));
        var original = ValidateBudgetCancellation(transactionId);
        var reversal = BudgetTransaction.CreateCancellation(original, actorId, reason.Trim());
        AddBudgetTransaction(reversal);
        return reversal;
    }

    private void AddBudgetRequest(BudgetRequest request, User user)
    {
        if (request.UserId != user.Id)
        {
            throw new InvalidOperationException("この予算申請のユーザーIDは、リクエストを追加しようとしているユーザーのIDと一致しません。");
        }
        EnsureRequestMembership(user);
        _requests.Add(request);
        BudgetRevision = Guid.NewGuid();
    }

    public BudgetRequest CreateBudgetRequest(User user, Money amount, FiscalYear fiscalYear, string description, IEnumerable<string> evidenceFilePaths)
    {
        ArgumentNullException.ThrowIfNull(evidenceFilePaths);

        var request = new BudgetRequest(user.Id, amount, fiscalYear, description, evidenceFilePaths);
        AddBudgetRequest(request, user);
        return request;
    }

    public void UpdateBudgetRequestStatus(int requestId, RequestStatus newStatus)
    {
        var request = GetBudgetRequestById(requestId);
        request.UpdateStatus(newStatus);
        BudgetRevision = Guid.NewGuid();
    }

    public void UpdateBudgetRequestStatus(int requestId, RequestStatus newStatus, User changedBy)
    {
        var request = GetBudgetRequestById(requestId);
        request.UpdateStatus(newStatus, changedBy);
        BudgetRevision = Guid.NewGuid();
    }

    public decimal GetTotalBudgetForFiscalYear(FiscalYear fiscalYear)
    {
        var totalIncome = _budgetTransactions
            .Where(t => t.IsIncome && t.FiscalYear == fiscalYear)
            .Sum(t => t.Amount.Value);

        var totalExpense = _budgetTransactions
            .Where(t => !t.IsIncome && t.FiscalYear == fiscalYear)
            .Sum(t => t.Amount.Value);

        return totalIncome - totalExpense;
    }

    public bool IsWithinBudgetLimit(Money amount, FiscalYear fiscalYear)
    {
        decimal currentBudget = GetTotalBudgetForFiscalYear(fiscalYear);
        var pendingTotal = _requests
            .Where(r => r.FiscalYear == fiscalYear && r.GetCurrentStatus() == RequestStatus.Pending)
            .Sum(r => r.Amount.Value);

        return currentBudget - pendingTotal - amount.Value >= 0;
    }

    public IReadOnlyCollection<BudgetRequest> GetRequestsByStatus(RequestStatus status)
    {
        return _requests.Where(r => r.GetCurrentStatus() == status).ToList();
    }

    public void EnsureRequestMembership(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsMemberOf(Id))
        {
            throw new BudgetRequestMembershipException(user.GroupIds, Id, Name);
        }
    }

    private BudgetRequest GetBudgetRequestById(int requestId)
    {
        var request = _requests.FirstOrDefault(r => r.Id == requestId);
        if (request == null)
        {
            throw new ArgumentNullException(nameof(requestId), "Budget request not found");
        }
        return request;
    }

}
