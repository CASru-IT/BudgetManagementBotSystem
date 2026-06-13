using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.Application.UseCases.UserManagement
{
    public class UserQueryUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;

        public UserQueryUseCase(IUserRepository userRepository, IGroupRepository groupRepository)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
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
