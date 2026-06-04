using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Application.Interface;

namespace BudgetManagementBotSystem.Application.UseCases.Groups;

public class DeleteGroupUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGroupUseCase(IGroupRepository groupRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> ExecuteAsync(ulong callerDiscordId, int groupId)
    {
        var caller = await _userRepository.GetByDiscordUserIdAsync(callerDiscordId);
        if (caller == null) throw new ArgumentException("Discord user not registered");
        if (caller.Role != AccountRole.Admin) throw new UnauthorizedAccessException("This action requires admin privileges");

        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentException("Group not found");
        var groupName = group.Name;

        var users = await _userRepository.GetAllAsync();
        if (users != null)
        {
            foreach (var u in users.Where(u => u.GroupId == groupId))
            {
                u.ChangeGroupId(null);
            }
        }

        await _groupRepository.DeleteAsync(groupId);
        await _unitOfWork.SaveChangesAsync();

        return groupName;
    }
}
