namespace BudgetManagementBotSystem.Application.Interface;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
