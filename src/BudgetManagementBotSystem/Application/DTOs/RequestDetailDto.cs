using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Application.DTOs;

public class RequestDetailDto
{
    public BudgetRequest? Request { get; }
    public int? GroupId { get; }
    public string? GroupName { get; }
    public IReadOnlyList<UploadedEvidenceDto> Evidences { get; }
    public IReadOnlyList<string> MissingEvidencePaths { get; }

    public RequestDetailDto(
        BudgetRequest? request,
        int? groupId,
        string? groupName,
        IReadOnlyList<UploadedEvidenceDto> evidences,
        IReadOnlyList<string> missingEvidencePaths)
    {
        Request = request;
        GroupId = groupId;
        GroupName = groupName;
        Evidences = evidences;
        MissingEvidencePaths = missingEvidencePaths;
    }
}