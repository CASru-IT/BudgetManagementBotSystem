namespace BudgetManagementBotSystem.Application.DTOs;

public sealed class ApprovedRequestNotificationDto
{
    public ulong RequesterDiscordUserId { get; }
    public int RequestId { get; }
    public string GroupName { get; }
    public decimal Amount { get; }
    public string Description { get; }
    public string ApproverName { get; }
    public ulong ApproverDiscordUserId { get; }

    public ApprovedRequestNotificationDto(
        ulong requesterDiscordUserId,
        int requestId,
        string groupName,
        decimal amount,
        string description,
        string approverName,
        ulong approverDiscordUserId)
    {
        RequesterDiscordUserId = requesterDiscordUserId;
        RequestId = requestId;
        GroupName = groupName;
        Amount = amount;
        Description = description;
        ApproverName = approverName;
        ApproverDiscordUserId = approverDiscordUserId;
    }
}