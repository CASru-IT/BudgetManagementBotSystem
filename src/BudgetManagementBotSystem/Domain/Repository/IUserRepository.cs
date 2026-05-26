using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Repository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByDiscordUserIdAsync(ulong discordUserId);
    Task<bool> IsUserExistsAsync(int userId);
    Task AddAsync(User user);
    Task<List<User>?> GetAllAsync();
    Task DeleteAsync(int userId);
}
