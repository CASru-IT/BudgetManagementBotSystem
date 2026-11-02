using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class BudgetTransaction
{
    public int Id { get; private set; }
    public bool IsIncome { get; private set; }
    public Money Amount { get; private set; } = null!;
    public FiscalYear FiscalYear { get; private set; } = null!;
    public DateTime TransactionDate { get; private set; }
    public BudgetTransactionKind Kind { get; private set; }
    public int? ReversesTransactionId { get; private set; }
    public int? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }

    private BudgetTransaction()
    {
    }

    public BudgetTransaction(bool isIncome, decimal amount, FiscalYear fiscalYear, BudgetTransactionKind kind = BudgetTransactionKind.Legacy)
    {
        IsIncome = isIncome;
        Amount = new Money(amount);
        FiscalYear = fiscalYear;
        TransactionDate = DateTime.UtcNow;
        Kind = kind;
    }

    internal static BudgetTransaction CreateCancellation(BudgetTransaction original, int actorId, string reason)
        => new(false, original.Amount.Value, new FiscalYear(original.FiscalYear.Year, original.FiscalYear.StartMonth), BudgetTransactionKind.BudgetCancellation)
        {
            ReversesTransactionId = original.Id,
            CancelledByUserId = actorId,
            CancellationReason = reason
        };
}
