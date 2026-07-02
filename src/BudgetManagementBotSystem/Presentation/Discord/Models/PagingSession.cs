namespace BudgetManagementBotSystem.Presentation.Discord.Models;

public class PagingSession
{
    public string Token { get; set; } = string.Empty;
    public ulong OwnerDiscordUserId { get; set; }
    public PagingTarget Target { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? GroupId { get; set; }
    public int? FiscalYear { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public enum PagingTarget
{
    UsageHistory,
    RequestList,
    PendingList,
    UserList,
    GroupList,
    GroupMembers,
    AllHistory
}
