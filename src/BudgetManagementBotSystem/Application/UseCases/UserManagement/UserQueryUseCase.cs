using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.UserManagement
{
    public class UserQueryUseCase
    {
        private readonly IUserRepository _userRepository;

        public UserQueryUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<List<User>> ListUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users ?? new List<User>();
        }

        public async Task<User?> GetByDiscordIdAsync(ulong discordUserId)
        {
            return await _userRepository.GetByDiscordUserIdAsync(discordUserId);
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<List<User>> GetMembersByGroupIdAsync(int groupId)
        {
            var users = await _userRepository.GetAllAsync();
            if (users == null) return new List<User>();
            return users.Where(u => u.GroupId.HasValue && u.GroupId.Value == groupId).ToList();
        }
    }
}
