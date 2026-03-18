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

        if (group == null) throw new InvalidOperationException("Group not found");

        FiscalYear currentFiscalYear = new FiscalYear(DateTime.Now.Year, _configuration.GetValue<int>("FiscalYearStartMonth"));
        BudgetTransaction transaction = new BudgetTransaction(true, amount, currentFiscalYear);
        group.AddBudgetTransaction(transaction);
    }
}
