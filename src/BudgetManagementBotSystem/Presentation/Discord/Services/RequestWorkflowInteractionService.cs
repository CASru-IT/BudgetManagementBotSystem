using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class RequestWorkflowInteractionService
{
    private readonly ApproveBudgetRequestUseCase _approveUseCase;
    private readonly RejectBudgetRequestUseCase _rejectUseCase;
    private readonly RequestQueryUseCase _requestQueryUseCase;
    private readonly DiscordRequestNotificationService _notificationService;
    private readonly BudgetActivityChannel _activityChannel;
    private readonly ILogger<RequestWorkflowInteractionService> _logger;

    public RequestWorkflowInteractionService(
        ApproveBudgetRequestUseCase approveUseCase,
        RejectBudgetRequestUseCase rejectUseCase,
        RequestQueryUseCase requestQueryUseCase,
        DiscordRequestNotificationService notificationService,
        BudgetActivityChannel activityChannel,
        ILogger<RequestWorkflowInteractionService> logger)
    {
        _approveUseCase = approveUseCase;
        _rejectUseCase = rejectUseCase;
        _requestQueryUseCase = requestQueryUseCase;
        _notificationService = notificationService;
        _activityChannel = activityChannel;
        _logger = logger;
    }

    public async Task ApproveAsync(int requestId, ulong actorDiscordUserId, Func<bool, Task> respondAsync)
    {
        var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(actorDiscordUserId);
        var request = await _requestQueryUseCase.GetRequestSummaryAsync(requestId);

        await _approveUseCase.ExecuteAsync(request.GroupId, requestId, userId);
        await Task.WhenAll(
            _activityChannel.NotifyRequestAsync("予算申請が承認されました", actorDiscordUserId, request),
            RespondAfterDmAsync(requestId, _notificationService.NotifyApprovedAsync(requestId, userId), respondAsync));
    }

    public async Task RejectAsync(int requestId, ulong actorDiscordUserId, string reason, Func<bool, Task> respondAsync)
    {
        var userId = await _requestQueryUseCase.GetLocalUserIdByDiscordIdAsync(actorDiscordUserId);
        var request = await _requestQueryUseCase.GetRequestSummaryAsync(requestId);

        await _rejectUseCase.ExecuteAsync(request.GroupId, requestId, userId);
        await Task.WhenAll(
            _activityChannel.NotifyRequestAsync("予算申請が却下されました", actorDiscordUserId, request),
            RespondAfterDmAsync(requestId, _notificationService.NotifyRejectedAsync(requestId, userId, reason), respondAsync));
    }

    private async Task RespondAfterDmAsync(int requestId, Task<bool> dm, Func<bool, Task> respondAsync)
    {
        bool sent;
        try
        {
            sent = await dm;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not notify the requester by DM after deciding request {RequestId}.", requestId);
            sent = false;
        }
        await respondAsync(sent);
    }
}
