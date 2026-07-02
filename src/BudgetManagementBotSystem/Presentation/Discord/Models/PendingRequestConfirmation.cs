using BudgetManagementBotSystem.Application.DTOs;

namespace BudgetManagementBotSystem.Presentation.Discord.Models;

public class PendingRequestConfirmation
{
    public string Token { get; set; } = string.Empty;
    public ulong RequesterDiscordUserId { get; set; }
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<UploadedEvidenceDto> EvidenceFiles { get; set; } = new();
    public DateTimeOffset ExpiresAt { get; set; }
}
