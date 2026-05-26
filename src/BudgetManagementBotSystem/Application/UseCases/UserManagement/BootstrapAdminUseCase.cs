using System;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.Options;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using Microsoft.Extensions.Options;

namespace BudgetManagementBotSystem.Application.UseCases.UserManagement;

public class BootstrapAdminUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AdminBootstrapOptions _options;

    public BootstrapAdminUseCase(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<AdminBootstrapOptions> options)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task ExecuteAsync(ulong discordUserId, string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("Admin bootstrap password is not configured.");
        }

        if (!string.Equals(password, _options.Password, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid admin bootstrap password.");
        }

        var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
        if (user == null)
        {
            var bootstrapName = string.IsNullOrWhiteSpace(userName)
                ? $"Discord User {discordUserId}"
                : userName.Trim();

            user = new User(bootstrapName, discordUserId, AccountRole.Admin);
            await _userRepository.AddAsync(user);
        }
        else
        {
            user.ChangeRole(AccountRole.Admin);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
