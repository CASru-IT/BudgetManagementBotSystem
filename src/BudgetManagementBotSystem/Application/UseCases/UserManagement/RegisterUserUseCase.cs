using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.UserManagement;

public class RegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(string name, ulong discordUserId, AccountRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("User name is required", nameof(name));
        }

        User user = new User(name.Trim(), discordUserId, role);
        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}
