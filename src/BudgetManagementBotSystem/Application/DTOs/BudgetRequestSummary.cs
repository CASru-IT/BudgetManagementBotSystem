namespace BudgetManagementBotSystem.Application.DTOs;

public sealed record BudgetRequestSummary(int RequestId, int GroupId, string GroupName, decimal Amount);
