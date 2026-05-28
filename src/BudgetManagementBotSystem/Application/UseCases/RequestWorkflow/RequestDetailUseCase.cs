using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Repository;
using System.IO;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class RequestDetailUseCase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IFileStorage _fileStorage;

        public RequestDetailUseCase(IGroupRepository groupRepository, IFileStorage fileStorage)
        {
            _groupRepository = groupRepository;
            _fileStorage = fileStorage;
        }

        public async Task<RequestDetailDto> GetByIdAsync(int requestId)
        {
            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) return new RequestDetailDto(null, null, null, Array.Empty<UploadedEvidenceDto>(), Array.Empty<string>());

            foreach (var g in groups)
            {
                var r = g.Requests.FirstOrDefault(x => x.Id == requestId);
                if (r != null)
                {
                    var evidences = new List<UploadedEvidenceDto>();
                    var missingEvidencePaths = new List<string>();

                    foreach (var evidence in r.Evidences)
                    {
                        try
                        {
                            await using var stream = await _fileStorage.GetFileAsync(evidence.FilePath);
                            using var memoryStream = new MemoryStream();
                            await stream.CopyToAsync(memoryStream);
                            evidences.Add(new UploadedEvidenceDto(Path.GetFileName(evidence.FilePath), memoryStream.ToArray()));
                        }
                        catch
                        {
                            missingEvidencePaths.Add(evidence.FilePath);
                        }
                    }

                    return new RequestDetailDto(r, g.Id, g.Name, evidences, missingEvidencePaths);
                }
            }

            return new RequestDetailDto(null, null, null, Array.Empty<UploadedEvidenceDto>(), Array.Empty<string>());
        }
    }
}
