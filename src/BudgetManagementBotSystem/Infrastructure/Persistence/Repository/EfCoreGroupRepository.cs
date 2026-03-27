using Microsoft.EntityFrameworkCore;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.InfraStructure.Persistence;

namespace BudgetManagementBotSystem.Infrastructure.Persistence.Repository;

public class EfCoreGroupRepository : IGroupRepository
{
    private readonly BudgetManagementDbContext _context;

    public EfCoreGroupRepository(BudgetManagementDbContext context)
    {
        _context = context;
    }

    public async Task<Group?> GetByIdAsync(int id)
    {
        return await _context.Groups
            .Include(g => g.BudgetTransactions)
            .Include(g => g.Requests)
                .ThenInclude(r => r.Evidences)
            .Include(g => g.Requests)
                .ThenInclude(r => r.StatusHistory)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<List<Group>?> GetAllAsync()
    {
        return await _context.Groups
            .Include(g => g.BudgetTransactions)
            .Include(g => g.Requests)
                .ThenInclude(r => r.Evidences)
            .Include(g => g.Requests)
                .ThenInclude(r => r.StatusHistory)
            .ToListAsync();
    }

    public async Task AddAsync(Group group)
    {
        await _context.Groups.AddAsync(group);
    }

    public async Task DeleteAsync(int groupId)
    {
        var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
        {
            return;
        }

        _context.Groups.Remove(group);
    }
}
