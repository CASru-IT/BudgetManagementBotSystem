using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class RejectBudgetRequestUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    public RejectBudgetRequestUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
    }

    public async Task ExecuteAsync(int groupId, int requestId, int changedByUserId)
    {
        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        User? changedByUser = await _userRepository.GetByIdAsync(changedByUserId);
        if (changedByUser == null) throw new ArgumentNullException(nameof(changedByUserId), "User not found");

        BudgetRequest? request = group.Requests.FirstOrDefault(r => r.Id == requestId);
        if (request == null) throw new ArgumentNullException(nameof(requestId), "Budget request not found");

        request.UpdateStatus(RequestStatus.Rejected, changedByUser);

        await _groupRepository.UpdateAsync(group);
    }
}
