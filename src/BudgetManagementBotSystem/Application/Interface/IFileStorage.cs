namespace BudgetManagementBotSystem.Application.Interface;

public interface IFileStorage
{
    Task<string> SaveFileAsync(string fileName, Stream fileStream);
    Task<Stream> GetFileAsync(string filePath);
    Task DeleteFileAsync(string filePath);
}
