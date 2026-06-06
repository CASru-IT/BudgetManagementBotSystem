using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class NotifyRejectedRequestUseCase
{
    private readonly RequestDetailUseCase _requestDetailUseCase;
    private readonly IUserRepository _userRepository;

    public NotifyRejectedRequestUseCase(
        RequestDetailUseCase requestDetailUseCase,
        IUserRepository userRepository)
    {
        _requestDetailUseCase = requestDetailUseCase;
        _userRepository = userRepository;
    }

    public async Task<RejectedRequestNotificationDto?> ExecuteAsync(int requestId, int rejecterUserId)
    {
        var rejecter = await _userRepository.GetByIdAsync(rejecterUserId);
        if (rejecter == null)
        {
            return null;
        }

        var detail = await _requestDetailUseCase.GetByIdAsync(requestId);
        var request = detail.Request;
        if (request == null)
        {
            return null;
        }

        var requester = await _userRepository.GetByIdAsync(request.UserId);
        if (requester == null)
        {
            return null;
        }

        var groupLabel = string.IsNullOrWhiteSpace(detail.GroupName) ? "不明" : detail.GroupName;
        return new RejectedRequestNotificationDto(
            requester.DiscordUserId,
            request.Id,
            groupLabel,
            request.Amount.Value,
            request.Description,
            rejecter.Name,
            rejecter.DiscordUserId);
    }
}
