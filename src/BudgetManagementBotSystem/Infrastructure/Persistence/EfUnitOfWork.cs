using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.InfraStructure.Persistence;

namespace BudgetManagementBotSystem.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly BudgetManagementDbContext _context;

    public EfUnitOfWork(BudgetManagementDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
