using BudgetManagementBotSystem.Application.Interface;

namespace BudgetManagementBotSystem.Infrastructure.FileStorage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _storageRootPath;

    public LocalFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        //ルートがついていないパスを入れないといけない
        string configuredPath = configuration["EvidenceStorage:BasePath"] ?? "data/evidences";
        
        //環境のルートパスと結合して絶対パスを作成
        _storageRootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

        Directory.CreateDirectory(_storageRootPath);
    }

    public async Task<string> SaveFileAsync(string fileName, Stream fileStream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileStream);

        string safeOriginalFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeOriginalFileName))
        {
            safeOriginalFileName = "evidence.bin";
        }

        string savedFileName = $"{Guid.NewGuid():N}_{safeOriginalFileName}";
        string destinationPath = Path.Combine(_storageRootPath, savedFileName);

        await using var destinationStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await fileStream.CopyToAsync(destinationStream);

        return destinationPath;
    }

    public async Task<Stream> GetFileAsync(string filePath)
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
            
        return stream;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string normalizedPath = Path.GetFullPath(filePath);

        if (File.Exists(normalizedPath))
        {
            File.Delete(normalizedPath);
        }

        return;
    }
}
