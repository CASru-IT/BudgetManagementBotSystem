using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;

public class SubmitBudgetRequestUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public SubmitBudgetRequestUseCase(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<int> ExecuteAsync(
        int userId,
        int groupId,
        decimal amount,
        string description,
        IEnumerable<UploadedEvidenceDto> evidenceFiles)
    {
        ArgumentNullException.ThrowIfNull(evidenceFiles);

        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new ArgumentNullException(nameof(userId), "User not found");

        Group? group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null) throw new ArgumentNullException(nameof(groupId), "Group not found");

        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");

        var requestAmount = new Money(amount);
        var fiscalYear = new FiscalYear(_configuration.GetValue<int>("FiscalYearStartMonth:Month"));

        if (!group.IsWithinBudgetLimit(requestAmount, fiscalYear))
        {
            throw new BudgetLimitExceededException("現在の予算上限を超えています。申請は作成されませんでした。");
        }

        var finalEvidencePaths = new List<string>();

        foreach (var evidence in evidenceFiles)
        {
            if (evidence == null)
            {
                continue;
            }

            try
            {
                using var stream = new MemoryStream(evidence.Content, writable: false);
                var savedPath = await _fileStorage.SaveFileAsync(evidence.FileName, stream);
                finalEvidencePaths.Add(savedPath);
            }
            catch
            {
                // skip failed evidence save
            }
        }

        int requestId = group.CreateBudgetRequest(
            user,
            requestAmount,
            fiscalYear,
            description,
            finalEvidencePaths);

        await _unitOfWork.SaveChangesAsync();
        return finalEvidencePaths.Count;
    }
}
