using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Application.DTOs;

public class RequestDetailDto
{
    public BudgetRequest? Request { get; }
    public int? GroupId { get; }
    public IReadOnlyList<UploadedEvidenceDto> Evidences { get; }
    public IReadOnlyList<string> MissingEvidencePaths { get; }

    public RequestDetailDto(
        BudgetRequest? request,
        int? groupId,
        IReadOnlyList<UploadedEvidenceDto> evidences,
        IReadOnlyList<string> missingEvidencePaths)
    {
        Request = request;
        GroupId = groupId;
        Evidences = evidences;
        MissingEvidencePaths = missingEvidencePaths;
    }
}