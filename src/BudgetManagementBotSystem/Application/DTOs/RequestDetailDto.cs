using BudgetManagementBotSystem.Domain.Entities;

namespace BudgetManagementBotSystem.Application.DTOs;

public class RequestDetailDto
{
    public BudgetRequest? Request { get; }
    public int? GroupId { get; }
    public string? GroupName { get; }
    public string? RequesterName { get; }
    public ulong? RequesterDiscordUserId { get; }
    public IReadOnlyList<UploadedEvidenceDto> Evidences { get; }
    public IReadOnlyList<string> MissingEvidencePaths { get; }

    public RequestDetailDto(
        BudgetRequest? request,
        int? groupId,
        string? groupName,
        string? requesterName,
        ulong? requesterDiscordUserId,
        IReadOnlyList<UploadedEvidenceDto> evidences,
        IReadOnlyList<string> missingEvidencePaths)
    {
        Request = request;
        GroupId = groupId;
        GroupName = groupName;
        RequesterName = requesterName;
        RequesterDiscordUserId = requesterDiscordUserId;
        Evidences = evidences;
        MissingEvidencePaths = missingEvidencePaths;
    }
}
