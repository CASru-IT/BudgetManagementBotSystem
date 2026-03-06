using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class RequestStatusHistory
{
    public int Id { get; private set; }
    public RequestStatus ChangedStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }

    public RequestStatusHistory(RequestStatus changedStatus, DateTime changedAt)
    {
        ChangedStatus = changedStatus;
        ChangedAt = changedAt;
    }
}
