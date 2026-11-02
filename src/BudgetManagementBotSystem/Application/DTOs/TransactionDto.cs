using System;

namespace BudgetManagementBotSystem.Application.DTOs
{
    public class TransactionDto
    {
        public int TransactionId { get; set; }
        public Domain.Enums.BudgetTransactionKind Kind { get; set; }
        public int? ReversesTransactionId { get; set; }
        public int? ReversedByTransactionId { get; set; }
        public int? CancelledByUserId { get; set; }
        public string? CancellationReason { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool IsIncome { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public int FiscalYear { get; set; }
    }
}
