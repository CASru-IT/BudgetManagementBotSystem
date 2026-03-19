using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.ValueObjects;

namespace BudgetManagementBotSystem.Application.UseCases;

public class IncreaseBudgetLimitUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IConfiguration _configuration;

    public IncreaseBudgetLimitUseCase(IGroupRepository groupRepository, IConfiguration configuration)
    {
        _groupRepository = groupRepository;
        _configuration = configuration;
    }
    public async Task ExecuteAsync(int groupId, decimal amount)
    {
        Group? group = await _groupRepository.GetByIdAsync(groupId);

        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");

        FiscalYear currentFiscalYear = new FiscalYear(_configuration.GetValue<int>("FiscalYearStartMonth:Month"));
        BudgetTransaction transaction = new BudgetTransaction(true, amount, currentFiscalYear);
        group.AddBudgetTransaction(transaction);

        await _groupRepository.UpdateAsync(group);
    }
}
