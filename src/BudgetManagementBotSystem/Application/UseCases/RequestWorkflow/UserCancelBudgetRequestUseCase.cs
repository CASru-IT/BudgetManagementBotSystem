using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Enums;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class UserCancelBudgetRequestUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserCancelBudgetRequestUseCase(IGroupRepository groupRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(int groupId, int requestId, int userId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new ArgumentNullException(nameof(userId), "User not found");

        var request = group.Requests.FirstOrDefault(r => r.Id == requestId);
        if (request == null) throw new ArgumentNullException(nameof(requestId), "Request not found in group");

        if (request.UserId != userId) throw new UnauthorizedAccessException("ユーザーはこの申請の作成者ではありません。");

        var current = request.StatusHistory.Last().ChangedStatus;
        if (current != RequestStatus.Pending) throw new InvalidOperationException("この申請は取消できる状態ではありません。");

        group.UpdateBudgetRequestStatus(requestId, RequestStatus.Cancelled);

        await _unitOfWork.SaveChangesAsync();
    }
}
