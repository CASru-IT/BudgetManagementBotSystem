using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class Group
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public List<BudgetTransaction> BudgetTransactions { get; private set; } = new List<BudgetTransaction>();
    public List<BudgetRequest> Requests { get; private set; } = new List<BudgetRequest>();

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
        BudgetTransactions.Add(transaction);
    }

    public void AddBudgetRequest(BudgetRequest request, User user)
    {
        if (request.UserId != user.Id)
        {
            throw new InvalidOperationException("この予算申請のユーザーIDは、リクエストを追加しようとしているユーザーのIDと一致しません。");
        }
        else if (!IsUserMemberOfGroup(user))
        {
            throw new InvalidOperationException("この予算申請のユーザーはグループのメンバーではありません。");
        }
        Requests.Add(request);
    }

    public decimal GetTotalBudgetForFiscalYear(FiscalYear fiscalYear)
    {
        var totalIncome = BudgetTransactions
            .Where(t => t.IsIncome && t.FiscalYear == fiscalYear)
            .Sum(t => t.Amount.Value);

        var totalExpense = BudgetTransactions
            .Where(t => !t.IsIncome && t.FiscalYear == fiscalYear)
            .Sum(t => t.Amount.Value);

        return totalIncome - totalExpense;
    }

    public List<BudgetRequest> GetRequestsByStatus(RequestStatus status)
    {
        return Requests.Where(r => r.StatusHistory.Last().ChangedStatus == status).ToList();
    }

    private bool IsUserMemberOfGroup(User user)
    {
        return user.GroupId == Id;
    }
}
