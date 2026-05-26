using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.Groups;

public class RegisterGroupUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterGroupUseCase(IGroupRepository groupRepository, IUnitOfWork unitOfWork)
    {
        _groupRepository = groupRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name is required", nameof(name));
        }

        Group group = new Group(name.Trim());
        await _groupRepository.AddAsync(group);
        await _unitOfWork.SaveChangesAsync();
    }
}
