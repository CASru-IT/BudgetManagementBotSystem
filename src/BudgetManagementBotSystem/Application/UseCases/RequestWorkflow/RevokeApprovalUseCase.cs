using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
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

            if (actingUser.Role != BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant)
            {
                throw new UnauthorizedAccessException("This action requires accountant privileges");
            }

            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) throw new ArgumentException("No groups available");

            Group? group = null;
            BudgetRequest? request = null;
            foreach (var g in groups)
            {
                var r = g.Requests.FirstOrDefault(x => x.Id == requestId);
                if (r != null)
                {
                    group = g;
                    request = r;
                    break;
                }
            }

            if (request == null) throw new ArgumentException("Request not found", nameof(requestId));
            if (group == null) throw new ArgumentException("Group not found for request", nameof(requestId));

            var currentStatus = request.GetCurrentStatus();
            if (currentStatus != RequestStatus.Approved)
            {
                throw new InvalidOperationException($"Request {requestId} is not approved.");
            }

            request.UpdateStatus(RequestStatus.ApprovalCancelled, actingUser);
            group.AddBudgetTransaction(new BudgetTransaction(true, request.Amount.Value, request.FiscalYear));

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
