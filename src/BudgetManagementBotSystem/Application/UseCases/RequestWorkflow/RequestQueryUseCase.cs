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

        public async Task<int> GetGroupIdByRequestIdAsync(int requestId)
        {
            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) throw new ArgumentException("No groups available");

            foreach (var g in groups)
            {
                var match = g.Requests.FirstOrDefault(r => r.Id == requestId);
                if (match != null)
                {
                    return g.Id;
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
