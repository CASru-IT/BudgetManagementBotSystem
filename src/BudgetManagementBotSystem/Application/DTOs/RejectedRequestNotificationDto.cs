namespace BudgetManagementBotSystem.Application.DTOs;

public sealed class RejectedRequestNotificationDto
{
    public ulong RequesterDiscordUserId { get; }
    public int RequestId { get; }
    public string GroupName { get; }
    public decimal Amount { get; }
    public string Description { get; }
    public string RejecterName { get; }
    public ulong RejecterDiscordUserId { get; }
    public string Reason { get; }

    public RejectedRequestNotificationDto(
        ulong requesterDiscordUserId,
        int requestId,
        string groupName,
        decimal amount,
        string description,
        string rejecterName,
        ulong rejecterDiscordUserId,
        string reason = "")
    {
        RequesterDiscordUserId = requesterDiscordUserId;
        RequestId = requestId;
        GroupName = groupName;
        Amount = amount;
        Description = description;
        RejecterName = rejecterName;
        RejecterDiscordUserId = rejecterDiscordUserId;
        Reason = reason;
    }
}
