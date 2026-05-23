using BudgetManagementBotSystem.Domain.Repository;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
    private readonly BudgetManagementBotSystem.Application.Interface.IFileStorage? _fileStorage;

    public SubmitBudgetRequestUseCase(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        BudgetManagementBotSystem.Application.Interface.IFileStorage? fileStorage = null)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task ExecuteAsync(
        int userId,
        int groupId,
        decimal amount,
        string description,
        IEnumerable<string> evidenceFilePaths)
    {
        ArgumentNullException.ThrowIfNull(evidenceFilePaths);

        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new ArgumentNullException(nameof(userId), "User not found");

        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");

        var requestAmount = new Money(amount);
        var fiscalYear = new FiscalYear(_configuration.GetValue<int>("FiscalYearStartMonth:Month"));

        // If file storage is available and evidenceFilePaths are local temp paths, save them to storage
        var savedPaths = new List<string>();
        if (_fileStorage != null && evidenceFilePaths.Any())
        {
            foreach (var path in evidenceFilePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    var stored = await _fileStorage.SaveFileAsync(Path.GetFileName(path), fs);
                    savedPaths.Add(stored);
                }
                catch
                {
                    // If saving a specific file fails, skip it but continue processing others
                }
            }
        }

        // If we saved files from temporary local paths, attempt to delete the temp files
        try
        {
            foreach (var original in evidenceFilePaths)
            {
                try
                {
                    if (File.Exists(original)) File.Delete(original);
                }
                catch
                {
                    // ignore delete errors
                }
            }
        }
        catch
        {
            // ignore
        }

        var finalEvidencePaths = savedPaths.Any() ? savedPaths : evidenceFilePaths;

        int requestId = group.CreateBudgetRequest(
            user,
            requestAmount,
            fiscalYear,
            description,
            finalEvidencePaths);

        if (!group.IsWithinBudgetLimit(requestAmount, fiscalYear))
        {
            group.UpdateBudgetRequestStatus(requestId, RequestStatus.Rejected);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
