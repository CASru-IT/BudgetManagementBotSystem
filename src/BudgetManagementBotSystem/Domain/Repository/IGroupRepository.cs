using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Domain.Repository;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(int groupId);
    Task<List<Group>?> GetAllAsync();
    Task AddAsync(Group group);
    Task UpdateAsync(Group group);
    Task DeleteAsync(int groupId);
}
