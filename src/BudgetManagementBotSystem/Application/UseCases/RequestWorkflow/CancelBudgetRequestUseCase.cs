using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class CancelBudgetRequestUseCase
{
    private readonly ApproveBudgetRequestUseCase _approveBudgetRequestUseCase;
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;

    public CancelBudgetRequestUseCase(ApproveBudgetRequestUseCase approveBudgetRequestUseCase, IGroupRepository groupRepository, IUserRepository userRepository)
    {
        _approveBudgetRequestUseCase = approveBudgetRequestUseCase;
        _groupRepository = groupRepository;
        _userRepository = userRepository;
    }

    public async Task ExecuteAsync(int groupId, int requestId, int changedByUserId)
    {
        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        User? changedByUser = await _userRepository.GetByIdAsync(changedByUserId);
        if (changedByUser == null) throw new ArgumentNullException(nameof(changedByUserId), "User not found");

        group.UpdateBudgetRequestStatus(requestId, Domain.Enums.RequestStatus.ApprovalCancelled, changedByUser);

        await _groupRepository.UpdateAsync(group);
    }
}
