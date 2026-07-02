using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class DiscordRequestNotificationService
{
    private readonly IUserRepository _userRepository;
    private readonly DiscordBotService _discordBotService;

    public DiscordRequestNotificationService(IUserRepository userRepository, DiscordBotService discordBotService)
    {
        _userRepository = userRepository;
        _discordBotService = discordBotService;
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
}
