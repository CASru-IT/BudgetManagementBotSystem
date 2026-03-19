using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Repository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByDiscordUserIdAsync(ulong discordUserId);
    Task<bool> IsUserExistsAsync(int userId);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int userId);
}
