using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class RequestWorkflowInteractionService
{
    private readonly ApproveBudgetRequestUseCase _approveUseCase;
    private readonly RejectBudgetRequestUseCase _rejectUseCase;
    private readonly RequestQueryUseCase _requestQueryUseCase;
    private readonly DiscordRequestNotificationService _notificationService;

    public RequestWorkflowInteractionService(
        ApproveBudgetRequestUseCase approveUseCase,
        RejectBudgetRequestUseCase rejectUseCase,
        RequestQueryUseCase requestQueryUseCase,
        DiscordRequestNotificationService notificationService)
    {
        _approveUseCase = approveUseCase;
        _rejectUseCase = rejectUseCase;
        _requestQueryUseCase = requestQueryUseCase;
        _notificationService = notificationService;
    }

    public async Task<bool> ApproveAsync(int requestId, ulong actorDiscordUserId)
    {
        var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(actorDiscordUserId);
        var groupId = await _requestQueryUseCase.GetGroupIdByRequestIdAsync(requestId);

        await _approveUseCase.ExecuteAsync(groupId, requestId, userId);
        return await _notificationService.NotifyApprovedAsync(requestId, userId);
    }

    public async Task<bool> RejectAsync(int requestId, ulong actorDiscordUserId, string reason)
    {
        var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(actorDiscordUserId);
        var groupId = await _requestQueryUseCase.GetGroupIdByRequestIdAsync(requestId);

        await _rejectUseCase.ExecuteAsync(groupId, requestId, userId);
        return await _notificationService.NotifyRejectedAsync(requestId, userId, reason);
    }
}
