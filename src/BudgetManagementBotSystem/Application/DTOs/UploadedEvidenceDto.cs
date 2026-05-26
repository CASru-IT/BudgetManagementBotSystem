namespace BudgetManagementBotSystem.Application.DTOs;

public class UploadedEvidenceDto
{
    public string FileName { get; }
    public byte[] Content { get; }

    public UploadedEvidenceDto(string fileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        FileName = fileName;
        Content = content;
    }
}