namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class BudgetLimitExceededException : Exception
{
    public BudgetLimitExceededException(string message) : base(message)
    {
    }
}