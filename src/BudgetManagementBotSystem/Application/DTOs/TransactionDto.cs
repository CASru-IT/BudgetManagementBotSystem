using System;

namespace BudgetManagementBotSystem.Application.DTOs
{
    public class TransactionDto
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool IsIncome { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public int FiscalYear { get; set; }
    }
}
