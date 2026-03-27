using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Application.Interface;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class SubmitBudgetRequestUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitBudgetRequestUseCase(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(int userId, int groupId, decimal amount, string description)
    {
        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new ArgumentNullException(nameof(userId), "User not found");

        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");

        var requestAmount = new Money(amount);
        var fiscalYear = new FiscalYear(_configuration.GetValue<int>("FiscalYearStartMonth:Month"));

        int requestId = group.CreateBudgetRequest(
            user,
            requestAmount,
            fiscalYear,
            description);

        if (!group.IsWithinBudgetLimit(requestAmount, fiscalYear))
        {
            group.UpdateBudgetRequestStatus(requestId, RequestStatus.Rejected);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
