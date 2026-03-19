using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Services;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class SubmitBudgetRequestUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly BudgetRequestBudgetLimitCheckService _budgetLimitCheckService;
    private readonly IConfiguration _configuration;

    public SubmitBudgetRequestUseCase(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        BudgetRequestBudgetLimitCheckService budgetLimitCheckService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _budgetLimitCheckService = budgetLimitCheckService;
        _configuration = configuration;
    }

    public async Task ExecuteAsync(int userId, int groupId, decimal amount, string description)
    {
        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new ArgumentNullException(nameof(userId), "User not found");

        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");

        BudgetRequest request = new BudgetRequest(user.Id, new Money(amount), new FiscalYear(_configuration.GetValue<int>("FiscalYearStartMonth:Month")), description);
        group.AddBudgetRequest(request, user);

        if (!_budgetLimitCheckService.IsWithinBudgetLimit(request, group)) request.UpdateStatus(RequestStatus.Rejected);

        await _groupRepository.UpdateAsync(group);
    }
}
