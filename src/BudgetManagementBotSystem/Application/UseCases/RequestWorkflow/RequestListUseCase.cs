using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class RequestListUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;

        public RequestListUseCase(IGroupRepository groupRepository, IUserRepository userRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
        }

        public async Task<PagedResult<PendingRequestDto>> ExecuteAsync(ulong discordUserId, string? status, int page = 1, int pageSize = 10, int? groupId = null)
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

            var allRequests = groups.SelectMany(g => g.Requests.Select(r => new PendingRequestDto
            {
                Id = r.Id,
                Amount = r.Amount.Value,
                RequestDate = r.RequestDate,
                Description = r.Description,
                GroupId = g.Id
            }));

            if (isPrivileged)
            {
                if (groupId.HasValue)
                {
                    allRequests = allRequests.Where(r => r.GroupId == groupId.Value);
                }
            }
            else
            {
                allRequests = allRequests.Where(r => r.GroupId == user.GroupId!.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<RequestStatus>(status, true, out var parsed))
                {
                    // Filter by status by looking up in groups' requests
                    allRequests = allRequests.Where(r => groups.First(g => g.Id == r.GroupId).Requests.First(req => req.Id == r.Id).StatusHistory.Last().ChangedStatus == parsed);
                }
                else
                {
                    return new PagedResult<PendingRequestDto> { Total = 0, Page = page, PageSize = pageSize };
                }
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
