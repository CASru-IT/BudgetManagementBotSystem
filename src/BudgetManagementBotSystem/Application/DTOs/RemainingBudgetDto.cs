namespace BudgetManagementBotSystem.Application.DTOs
{
    public class RemainingBudgetDto
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public decimal ActualBalance { get; set; }
        public decimal PendingTotal { get; set; }
        public decimal AvailableAfterPending { get; set; }

        public decimal TotalBudget
        {
            get => ActualBalance;
            set => ActualBalance = value;
        }

        public decimal ApprovedTotal { get; set; }

        public decimal Available
        {
            get => AvailableAfterPending;
            set => AvailableAfterPending = value;
        }

        public int FiscalYear { get; set; }
    }
}
