using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Repository;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace BudgetManagementBotSystem.Application.UseCases.Budget
{
    public class BudgetQueryUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public BudgetQueryUseCase(IGroupRepository groupRepository, IUserRepository userRepository, IConfiguration configuration)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<RemainingBudgetDto> GetRemainingBudgetAsync(ulong discordUserId, int? targetGroupId)
        {
            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("Discord user not registered");

            if (!user.GroupId.HasValue && !targetGroupId.HasValue && !IsPrivileged(user))
            {
                throw new UnauthorizedAccessException("User has no group and is not privileged");
            }

            int gid = targetGroupId ?? user.GroupId!.Value;
            var group = await _groupRepository.GetByIdAsync(gid);
            if (group == null) throw new ArgumentException("Group not found");

            int startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
            var fiscalYear = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(startMonth);

            decimal totalBudget = group.GetTotalBudgetForFiscalYear(fiscalYear);
            var pendingTotal = group.Requests.Where(r => r.StatusHistory.Last().ChangedStatus == BudgetManagementBotSystem.Domain.Enums.RequestStatus.Pending && r.FiscalYear == fiscalYear).Sum(r => r.Amount.Value);
            var available = totalBudget - pendingTotal;

            return new RemainingBudgetDto
            {
                GroupId = group.Id,
                GroupName = group.Name,
                TotalBudget = totalBudget,
                PendingTotal = pendingTotal,
                Available = available
            };
        }

        public async Task<PagedResult<TransactionDto>> GetUsageHistoryAsync(ulong discordUserId, int page = 1, int pageSize = 10, int? groupId = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            pageSize = Math.Min(pageSize, 50);

            var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) throw new ArgumentException("Discord user not registered");

            int targetGroupId = groupId ?? user.GroupId!.Value;
            var group = await _groupRepository.GetByIdAsync(targetGroupId);
            if (group == null) return new PagedResult<TransactionDto> { Total = 0, Page = page, PageSize = pageSize };

            var txs = group.BudgetTransactions.OrderByDescending(t => t.TransactionDate).ToList();
            var total = txs.Count;
            var items = txs.Skip((page - 1) * pageSize).Take(pageSize).Select(t => new TransactionDto
            {
                GroupId = group.Id,
                GroupName = group.Name,
                IsIncome = t.IsIncome,
                Amount = t.Amount.Value,
                TransactionDate = t.TransactionDate,
                FiscalYear = t.FiscalYear.Year
            }).ToList();

            return new PagedResult<TransactionDto>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<List<TransactionDto>> GetAllHistoryAsync(int take = 50)
        {
            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) return new List<TransactionDto>();

            var allTx = groups.SelectMany(g => g.BudgetTransactions.Select(t => new TransactionDto
            {
                GroupId = g.Id,
                GroupName = g.Name,
                IsIncome = t.IsIncome,
                Amount = t.Amount.Value,
                TransactionDate = t.TransactionDate,
                FiscalYear = t.FiscalYear.Year
            })).OrderByDescending(x => x.TransactionDate).Take(Math.Max(1, take)).ToList();

            return allTx;
        }

        private bool IsPrivileged(BudgetManagementBotSystem.Domain.Entities.User user)
        {
            var role = user.Role;
            return role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.President
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.GroupLeader;
        }
    }
}
