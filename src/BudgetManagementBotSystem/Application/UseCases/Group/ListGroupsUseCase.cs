using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.Groups;

public class ListGroupsUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;

    public ListGroupsUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<GroupListItemDto>> ExecuteAsync(ulong callerDiscordUserId)
    {
        var caller = await _userRepository.GetByDiscordUserIdAsync(callerDiscordUserId);
        if (caller == null)
        {
            throw new ArgumentException("Discord ユーザーが登録されていません。");
        }

        var groups = await _groupRepository.GetAllAsync();
        return groups?
            .OrderBy(group => group.Id)
            .Select(group => new GroupListItemDto(group.Id, group.Name))
            .ToList() ?? new List<GroupListItemDto>();
    }
}
