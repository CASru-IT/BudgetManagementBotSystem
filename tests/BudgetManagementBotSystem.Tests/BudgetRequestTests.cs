using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.ValueObjects;

namespace BudgetManagementBotSystem.Tests;

public class BudgetRequestTests
{
    private static BudgetRequest Create()
    => new BudgetRequest(
    userId: 1,
    amount: new Money(1000m),
    fiscalYear: new FiscalYear(4),
    description: "テスト申請");

    [Fact]
    public void Constructor_InitialStatus_IsPending()
    {
        var request = Create();

        Assert.Single(request.StatusHistory);
        Assert.Equal(RequestStatus.Pending, request.StatusHistory.Last().ChangedStatus);
    }

    [Fact]
    public void AddEvidence_AddsOneItem()
    {
        var request = Create();

        request.AddEvidence("evidence/path.png");

        Assert.Single(request.Evidences);
        Assert.Equal("evidence/path.png", request.Evidences[0].FilePath);
    }

    [Fact]
    public void UpdateStatus_PendingToApproved_IsAllowed()
    {
        var request = Create();

        request.UpdateStatus(RequestStatus.Approved);

        Assert.Equal(RequestStatus.Approved, request.StatusHistory.Last().ChangedStatus);
    }

    [Fact]
    public void UpdateStatus_PendingToApprovalCancelled_Throws()
    {
        var request = Create();

        Assert.Throws<InvalidOperationException>(() =>
        request.UpdateStatus(RequestStatus.ApprovalCancelled));
    }

    [Fact]
    public void UpdateStatus_ApprovedToApprovalCancelled_IsAllowed()
    {
        var request = Create();
        request.UpdateStatus(RequestStatus.Approved);

        request.UpdateStatus(RequestStatus.ApprovalCancelled);

        Assert.Equal(RequestStatus.ApprovalCancelled, request.StatusHistory.Last().ChangedStatus);
    }

    [Fact]
    public void UpdateStatus_RejectedToAny_Throws()
    {
        var request = Create();
        request.UpdateStatus(RequestStatus.Rejected);

        Assert.Throws<InvalidOperationException>(() =>
        request.UpdateStatus(RequestStatus.Approved));
    }
}
