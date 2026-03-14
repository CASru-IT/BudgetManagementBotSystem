using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;

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
    public List<RequestStatusChange> StatusHistory { get; private set; } = new List<RequestStatusChange>();

    public BudgetRequest(int userId, Money amount, FiscalYear fiscalYear, string description)
    {
        UserId = userId;
        Amount = amount;
        FiscalYear = fiscalYear;
        Description = description;
        RequestDate = DateTime.Now;
        if (StatusHistory.Count == 0)
        {
            StatusHistory.Add(new RequestStatusChange(RequestStatus.Pending, DateTime.Now));
        }
    }

    public void AddEvidence(string filePath)
    {
        Evidences.Add(new RequestEvidence(filePath));
    }
    public void UpdateStatus(RequestStatus newStatus)
    {
        CheckStatusTransition(newStatus);
        StatusHistory.Add(new RequestStatusChange(newStatus, DateTime.Now));
    }

    private void CheckStatusTransition(RequestStatus newStatus)
    {
        if (StatusHistory.Count == 0 && newStatus != RequestStatus.Pending)
        {
            throw new InvalidOperationException("最初のステータスはPendingでなければなりません。");
        }

        var currentStatus = StatusHistory.Last().ChangedStatus;

        switch (currentStatus)
        {
            case RequestStatus.Pending:
                if (newStatus != RequestStatus.Approved && newStatus != RequestStatus.Rejected)
                {
                    throw new InvalidOperationException("PendingからはApprovedまたはRejectedにしか遷移できません。");
                }
                break;
            case RequestStatus.Approved:
                if (newStatus != RequestStatus.ApprovalCancelled)
                {
                    throw new InvalidOperationException("ApprovedからはApprovalCancelledにしか遷移できません。");
                }
                break;
            case RequestStatus.Rejected:
                throw new InvalidOperationException("Rejectedからは遷移できません。");
            case RequestStatus.ApprovalCancelled:
                throw new InvalidOperationException("ApprovalCancelledからは遷移できません。");
            default:
                throw new InvalidOperationException("不正な現在のステータスです。");
        }
    }
}
