using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Application.Interface;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class RejectBudgetRequestUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    public RejectBudgetRequestUseCase(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(int groupId, int requestId, int changedByUserId)
    {
        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        User? changedByUser = await _userRepository.GetByIdAsync(changedByUserId);
        if (changedByUser == null) throw new ArgumentNullException(nameof(changedByUserId), "User not found");

        group.UpdateBudgetRequestStatus(requestId, RequestStatus.Rejected, changedByUser);

        await _unitOfWork.SaveChangesAsync();
    }
}
