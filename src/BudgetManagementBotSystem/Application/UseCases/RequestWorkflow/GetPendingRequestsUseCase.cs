using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class GetPendingRequestsUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;

        public GetPendingRequestsUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
        }

        public async Task<PagedResult<PendingRequestDto>> ExecuteAsync(ulong discordUserId, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            pageSize = Math.Min(pageSize, 50);

            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("Discord user not registered");

            var isPrivileged = user.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin
                || user.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant
                || user.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.President
                || user.Role == BudgetManagementBotSystem.Domain.Enums.AccountRole.GroupLeader;

            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) return new PagedResult<PendingRequestDto> { Total = 0, Page = page, PageSize = pageSize };

            var allRequests = groups
                .SelectMany(g => g.GetRequestsByStatus(RequestStatus.Pending)
                    .Select(r => new PendingRequestDto
            {
                Id = r.Id,
                Amount = r.Amount.Value,
                RequestDate = r.RequestDate,
                Description = r.Description,
                GroupId = g.Id
            }));

            if (!isPrivileged)
            {
                if (!user.GroupId.HasValue) return new PagedResult<PendingRequestDto> { Total = 0, Page = page, PageSize = pageSize };
                allRequests = allRequests.Where(r => r.GroupId == user.GroupId.Value);
            }

            var ordered = allRequests.OrderByDescending(r => r.RequestDate);
            var total = ordered.Count();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<PendingRequestDto>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }
    }
}
