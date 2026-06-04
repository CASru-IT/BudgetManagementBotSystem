using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.UserManagement
{
    public class UserCommandUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UserCommandUseCase(IUserRepository userRepository, IGroupRepository groupRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task DeactivateUserByDiscordIdAsync(ulong discordUserId)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("User not found");
            user.Deactivate();
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateUserRoleByDiscordIdAsync(ulong discordUserId, BudgetManagementBotSystem.Domain.Enums.AccountRole role)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("User not found");
            user.ChangeRole(role);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<string> AssignGroupByDiscordIdAsync(ulong discordUserId, int groupId)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("User not found");
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new ArgumentException("Group not found");
            var groupName = group.Name;
            user.ChangeGroupId(groupId);
            await _unitOfWork.SaveChangesAsync();

            return groupName;
        }

        public async Task UnassignGroupByDiscordIdAsync(ulong discordUserId)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("User not found");
            user.ChangeGroupId(null);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
