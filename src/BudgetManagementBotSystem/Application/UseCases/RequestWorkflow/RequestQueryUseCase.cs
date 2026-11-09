using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Repository;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class RequestQueryUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;

        public RequestQueryUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
        }

        public async Task<BudgetRequestSummary> GetRequestSummaryAsync(int requestId)
        {
            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) throw new ArgumentException("No groups available");

            foreach (var group in groups)
            {
                var request = group.Requests.FirstOrDefault(r => r.Id == requestId);
                if (request != null)
                {
                    return new BudgetRequestSummary(request.Id, group.Id, group.Name, request.Amount.Value);
                }
            }

            throw new ArgumentException("Request not found", nameof(requestId));
        }

        public async Task<int> GetLocalUserIdByDiscordIdAsync(ulong discordUserId)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("Discord user not registered");
            return user.Id;
        }
    }
}
