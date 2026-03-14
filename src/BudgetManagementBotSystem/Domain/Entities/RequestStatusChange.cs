using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class RequestStatusChange
{
    public int Id { get; private set; }
    public RequestStatus ChangedStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }

    public RequestStatusChange(RequestStatus changedStatus, DateTime changedAt)
    {
        ChangedStatus = changedStatus;
        ChangedAt = changedAt;
    }
}
