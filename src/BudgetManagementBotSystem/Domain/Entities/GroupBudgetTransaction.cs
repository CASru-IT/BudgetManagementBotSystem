using BudgetManagementBotSystem.Domain.ValueObjects;

namespace BudgetManagementBotSystem.Domain.Entities;

public class GroupBudgetTransaction
{
    public int Id { get; private set; }
    public int GroupId { get; private set; }
    public bool IsIncome { get; private set; }
    public Money Amount { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public DateTime TransactionDate { get; private set; }

    public GroupBudgetTransaction(int groupId, bool isIncome, decimal amount, FiscalYear fiscalYear)
    {
        GroupId = groupId;
        IsIncome = isIncome;
        Amount = new Money(amount);
        FiscalYear = fiscalYear;
        TransactionDate = DateTime.Now;
    }
}
