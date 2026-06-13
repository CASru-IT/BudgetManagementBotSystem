using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;

namespace BudgetManagementBotSystem.Application.UseCases.Budget;

public class AdminAddBudgetTransactionUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    public AdminAddBudgetTransactionUseCase(
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

    public async Task<AdminAddBudgetTransactionResult> ExecuteAsync(
        ulong discordUserId,
        int groupId,
        string transactionType,
        decimal amount,
        int? fiscalYear = null)
    {
        var user = await _userRepository.GetByDiscordUserIdAsync(discordUserId);
        if (user == null) throw new ArgumentException("Discord user not registered");
        if (user.Role != AccountRole.Admin) throw new UnauthorizedAccessException("This action requires admin privileges");

        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");

        var isIncome = ParseTransactionType(transactionType);
        var startMonth = _configuration.GetValue<int>("FiscalYearStartMonth:Month");
        var resolvedFiscalYear = fiscalYear.HasValue
            ? new FiscalYear(fiscalYear.Value, startMonth)
            : new FiscalYear(startMonth);

        group.AddBudgetTransaction(new BudgetTransaction(isIncome, amount, resolvedFiscalYear));
        await _unitOfWork.SaveChangesAsync();

        return new AdminAddBudgetTransactionResult(
            group.Id,
            group.Name,
            isIncome,
            amount,
            resolvedFiscalYear.Year,
            group.GetTotalBudgetForFiscalYear(resolvedFiscalYear));
    }

    private static bool ParseTransactionType(string transactionType)
    {
        if (string.Equals(transactionType, "income", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transactionType, "収入", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(transactionType, "expense", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transactionType, "支出", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ArgumentException("Transaction type must be income or expense.", nameof(transactionType));
    }
}

public sealed record AdminAddBudgetTransactionResult(
    int GroupId,
    string GroupName,
    bool IsIncome,
    decimal Amount,
    int FiscalYear,
    decimal ActualBalance);
