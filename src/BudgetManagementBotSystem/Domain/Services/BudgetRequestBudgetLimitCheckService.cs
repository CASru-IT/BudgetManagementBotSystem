using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Services;

public class BudgetRequestBudgetLimitCheckService
{
    public bool IsWithinBudgetLimit(BudgetRequest request, Group group)
    {
        decimal currentBudget = group.GetTotalBudgetForFiscalYear(request.FiscalYear);
        return currentBudget - request.Amount.Value >= 0;
    }
}
