using BudgetManagementBotSystem.Application.Interface;

namespace BudgetManagementBotSystem.Infrastructure.FileStorage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _storageRootPath;

    public LocalFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        string configuredPath = configuration["EvidenceStorage:BasePath"] ?? "data/evidences";
        
        _storageRootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

        Directory.CreateDirectory(_storageRootPath);
    }

    public async Task<string> SaveFileAsync(string fileName, Stream fileStream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileStream);

        string safeOriginalFileName = NormalizeFileName(fileName);

        string savedFileName = $"{Guid.NewGuid():N}_{safeOriginalFileName}";
        string destinationPath = Path.Combine(_storageRootPath, savedFileName);

        await using var destinationStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await fileStream.CopyToAsync(destinationStream);

        return destinationPath;
    }

    private static string NormalizeFileName(string fileName)
    {
        var originalName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return "evidence.bin";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();

        var safeName = new string(originalName
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray())
            .Trim(' ', '.');

        return string.IsNullOrWhiteSpace(safeName)
            ? "evidence.bin"
            : safeName;
    }

    public Task<Stream> GetFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string normalizedPath = Path.GetFullPath(filePath);
        Stream stream = new FileStream(
            normalizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
            
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string normalizedPath = Path.GetFullPath(filePath);

        if (File.Exists(normalizedPath))
        {
            File.Delete(normalizedPath);
        }

        return Task.CompletedTask;
    }
}
