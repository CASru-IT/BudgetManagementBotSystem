using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class RevokeApprovalUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public RevokeApprovalUseCase(IGroupRepository groupRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task ExecuteAsync(int requestId, ulong discordUserId)
        {
            var actingUser = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (actingUser == null) throw new ArgumentException("Discord user not registered");

            var isPrivileged = actingUser.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin
                || actingUser.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant
                || actingUser.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.President
                || actingUser.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.GroupLeader;

            if (!isPrivileged) throw new UnauthorizedAccessException("User is not privileged to revoke approvals");

            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) throw new ArgumentException("No groups available");

            (var group, var req) = (null as dynamic, null as dynamic);
            foreach (var g in groups)
            {
                var r = g.Requests.FirstOrDefault(x => x.Id == requestId);
                if (r != null)
                {
                    group = g;
                    req = r;
                    break;
                }
            }

            if (req == null) throw new ArgumentException("Request not found", nameof(requestId));

            var currentStatus = req.StatusHistory.Last().ChangedStatus;
            if (currentStatus != RequestStatus.Approved)
            {
                throw new InvalidOperationException($"Request {requestId} is not approved.");
            }

            req.UpdateStatus(RequestStatus.ApprovalCancelled, actingUser);

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
