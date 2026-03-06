using BudgetManagementBotSystem.Domain.ValueObjects;

namespace BudgetManagementBotSystem.Domain.Entities;

public class BudgetRequest
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public Money Amount { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public DateTime RequestDate { get; private set; }
    public string Description { get; private set; }
    public List<RequestEvidence> Evidences { get; private set; } = new List<RequestEvidence>();
    public List<RequestStatusHistory> StatusHistory { get; private set; } = new List<RequestStatusHistory>();

    public BudgetRequest(int userId, Money amount, FiscalYear fiscalYear, string description)
    {
        UserId = userId;
        Amount = amount;
        FiscalYear = fiscalYear;
        Description = description;
        RequestDate = DateTime.Now;
    }
}
