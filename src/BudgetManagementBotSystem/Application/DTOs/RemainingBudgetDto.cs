namespace BudgetManagementBotSystem.Application.DTOs
{
    public class RemainingBudgetDto
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public decimal TotalBudget { get; set; }
        public decimal PendingTotal { get; set; }
        public decimal Available { get; set; }
    }
}
