namespace BudgetManagementBotSystem.Application.DTOs;

public sealed record BudgetTransactionResult(
    int TransactionId,
    int GroupId,
    string GroupName,
    bool IsIncome,
    decimal Amount,
    int FiscalYear,
    decimal ActualBalance);
