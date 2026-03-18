using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Repository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string userId);
    Task<User?> GetByDiscordUserIdAsync(string discordUserId);
    Task<bool> IsUserExistsAsync(string userId);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string userId);
}
