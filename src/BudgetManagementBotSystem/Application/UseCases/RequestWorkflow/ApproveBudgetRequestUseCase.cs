using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class ApproveBudgetRequestUseCase
{
	private readonly IGroupRepository _groupRepository;
	private readonly IUserRepository _userRepository;

	public ApproveBudgetRequestUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
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

		group.UpdateBudgetRequestStatus(requestId, RequestStatus.Approved, changedByUser);

		await _groupRepository.UpdateAsync(group);
	}
}
