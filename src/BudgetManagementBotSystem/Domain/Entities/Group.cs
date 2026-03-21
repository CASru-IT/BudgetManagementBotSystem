using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class Group
{
    private readonly List<BudgetTransaction> _budgetTransactions = new List<BudgetTransaction>();
    private readonly List<BudgetRequest> _requests = new List<BudgetRequest>();

    public int Id { get; private set; }
    public string Name { get; private set; }
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
    }

    private void AddBudgetRequest(BudgetRequest request, User user)
    {
        if (request.UserId != user.Id)
        {
            throw new InvalidOperationException("この予算申請のユーザーIDは、リクエストを追加しようとしているユーザーのIDと一致しません。");
        }
        else if (!IsUserMemberOfGroup(user))
        {
            throw new InvalidOperationException("この予算申請のユーザーはグループのメンバーではありません。");
        }
        _requests.Add(request);
    }

    public int CreateBudgetRequest(User user, Money amount, FiscalYear fiscalYear, string description)
    {
        var request = new BudgetRequest(user.Id, amount, fiscalYear, description);
        AddBudgetRequest(request, user);
        return request.Id;
    }

    public void AddBudgetRequestEvidence(int requestId, string filePath)
    {
        var request = GetBudgetRequestById(requestId);
        request.AddEvidence(filePath);
    }

    public void UpdateBudgetRequestStatus(int requestId, RequestStatus newStatus)
    {
        var request = GetBudgetRequestById(requestId);
        request.UpdateStatus(newStatus);
    }

    public void UpdateBudgetRequestStatus(int requestId, RequestStatus newStatus, User changedBy)
    {
        var request = GetBudgetRequestById(requestId);
        request.UpdateStatus(newStatus, changedBy);
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
        return currentBudget - amount.Value >= 0;
    }

    public IReadOnlyCollection<BudgetRequest> GetRequestsByStatus(RequestStatus status)
    {
        return _requests.Where(r => r.StatusHistory.Last().ChangedStatus == status).ToList();
    }

    private bool IsUserMemberOfGroup(User user)
    {
        return user.GroupId == Id;
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
