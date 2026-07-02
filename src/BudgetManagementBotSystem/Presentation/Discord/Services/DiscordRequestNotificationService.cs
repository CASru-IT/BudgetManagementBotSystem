using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class DiscordRequestNotificationService
{
    private readonly IUserRepository _userRepository;
    private readonly DiscordBotService _discordBotService;
    private readonly NotifyApprovedRequestUseCase _notifyApprovedRequestUseCase;
    private readonly NotifyRejectedRequestUseCase _notifyRejectedRequestUseCase;

    public DiscordRequestNotificationService(
        IUserRepository userRepository,
        DiscordBotService discordBotService,
        NotifyApprovedRequestUseCase notifyApprovedRequestUseCase,
        NotifyRejectedRequestUseCase notifyRejectedRequestUseCase)
    {
        _userRepository = userRepository;
        _discordBotService = discordBotService;
        _notifyApprovedRequestUseCase = notifyApprovedRequestUseCase;
        _notifyRejectedRequestUseCase = notifyRejectedRequestUseCase;
    }

    public async Task<int> NotifyAccountantsAsync(
        int requestId,
        int groupId,
        decimal amount,
        string description,
        string requesterName,
        ulong requesterDiscordUserId)
    {
        var users = await _userRepository.GetAllAsync();
        if (users == null)
        {
            return 0;
        }

        var accountantUsers = users
            .Where(user => user.IsActive && user.Role == AccountRole.Accountant)
            .ToList();

        if (accountantUsers.Count == 0)
        {
            return 0;
        }

        var embed = DiscordEmbedFactory.BuildNewRequestAccountantDmEmbed(
            requestId,
            groupId,
            amount,
            description,
            requesterName,
            requesterDiscordUserId);

        var sendTasks = accountantUsers.Select(accountant => _discordBotService.SendDirectMessageAsync(accountant.DiscordUserId, embed));
        var results = await Task.WhenAll(sendTasks);
        return results.Count(result => result);
    }

    public async Task<bool> NotifyApprovedAsync(int requestId, int approverUserId)
    {
        var notification = await _notifyApprovedRequestUseCase.ExecuteAsync(requestId, approverUserId);
        if (notification == null)
        {
            return false;
        }

        return await _discordBotService.SendDirectMessageAsync(
            notification.RequesterDiscordUserId,
            DiscordEmbedFactory.BuildApprovedRequestDmEmbed(notification));
    }

    public async Task<bool> NotifyRejectedAsync(int requestId, int rejecterUserId, string reason)
    {
        var notification = await _notifyRejectedRequestUseCase.ExecuteAsync(requestId, rejecterUserId, reason);
        if (notification == null)
        {
            return false;
        }

        return await _discordBotService.SendDirectMessageAsync(
            notification.RequesterDiscordUserId,
            DiscordEmbedFactory.BuildRejectedRequestDmEmbed(notification));
    }
}
