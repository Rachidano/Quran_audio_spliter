using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quran.Application.Interfaces;

namespace Quran.Infrastructure.Services;

public class LocalStorageService : IStorageService
{
    private readonly string _baseStoragePath;
    private readonly string _publicUrlPrefix;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(IConfiguration configuration, ILogger<LocalStorageService> logger)
    {
        _logger = logger;

        // Configuration du répertoire de stockage local
        var configuredPath = configuration["Storage:BasePath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var appDir = Directory.GetCurrentDirectory();
            var candidate = Path.Combine(appDir, "..", "..", "data", "audio");
            _baseStoragePath = Directory.Exists(candidate) 
                ? Path.GetFullPath(candidate) 
                : Path.GetFullPath(Path.Combine(appDir, "data", "audio"));
        }
        else
        {
            _baseStoragePath = Path.GetFullPath(configuredPath);
        }

        _publicUrlPrefix = configuration["Storage:PublicUrlPrefix"] ?? "/audio";

        // Créer les sous-dossiers fondamentaux
        Directory.CreateDirectory(Path.Combine(_baseStoragePath, "sources"));
        Directory.CreateDirectory(Path.Combine(_baseStoragePath, "working"));
        Directory.CreateDirectory(Path.Combine(_baseStoragePath, "published"));
    }

    public async Task<string> SaveAudioAsync(Stream content, string relativeStorageKey, CancellationToken cancellationToken = default)
    {
        var physicalPath = GetPhysicalPath(relativeStorageKey);
        var dir = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
        _logger.LogInformation("Saved audio file to {Path}", physicalPath);

        return relativeStorageKey;
    }

    public Task<Stream?> GetAudioStreamAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
    {
        var physicalPath = GetPhysicalPath(relativeStorageKey);
        if (!File.Exists(physicalPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> FileExistsAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
    {
        var physicalPath = GetPhysicalPath(relativeStorageKey);
        return Task.FromResult(File.Exists(physicalPath));
    }

    public string GetPublicUrl(string relativeStorageKey)
    {
        var sanitizedKey = relativeStorageKey.Replace('\\', '/').TrimStart('/');
        return $"{_publicUrlPrefix.TrimEnd('/')}/{sanitizedKey}";
    }

    public string GetPhysicalPath(string relativeStorageKey)
    {
        var sanitizedKey = relativeStorageKey.Replace('/', Path.DirectorySeparatorChar)
                                            .Replace('\\', Path.DirectorySeparatorChar)
                                            .TrimStart(Path.DirectorySeparatorChar);
        return Path.Combine(_baseStoragePath, sanitizedKey);
    }
}
