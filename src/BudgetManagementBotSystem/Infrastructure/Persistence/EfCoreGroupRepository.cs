using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Infrastructure.Persistence;

class EfCoreGroupRepository : IGroupRepository
{
    public Task<Group?> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }
    public Task<List<Group>?> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(Group group)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(int groupId)
    {
        throw new NotImplementedException();
    }
}
