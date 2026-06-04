using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class NotifyApprovedRequestUseCase
{
    private readonly RequestDetailUseCase _requestDetailUseCase;
    private readonly IUserRepository _userRepository;

    public NotifyApprovedRequestUseCase(
        RequestDetailUseCase requestDetailUseCase,
        IUserRepository userRepository)
    {
        _requestDetailUseCase = requestDetailUseCase;
        _userRepository = userRepository;
    }

    public async Task<ApprovedRequestNotificationDto?> ExecuteAsync(int requestId, int approverUserId)
    {
        var approver = await _userRepository.GetByIdAsync(approverUserId);
        if (approver == null)
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
        return new ApprovedRequestNotificationDto(
            requester.DiscordUserId,
            request.Id,
            groupLabel,
            request.Amount.Value,
            request.Description,
            approver.Name,
            approver.DiscordUserId);
    }
}