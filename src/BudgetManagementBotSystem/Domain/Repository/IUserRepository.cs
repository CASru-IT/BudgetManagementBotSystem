using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Repository;

public interface IUserRepository
{
    Task<User?> GetUserInfoAsync(string userId);
    Task<bool> UserExistsAsync(string userId);
    Task AddUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(string userId);
}
