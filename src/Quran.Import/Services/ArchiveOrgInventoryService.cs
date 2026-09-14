using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;

namespace Quran.Import.Services;

public class ArchiveOrgInventoryService : IArchiveOrgInventoryService
{
    private const string ItemIdentifier = "z0603_202406x";
    private const string MetadataUrl = $"https://archive.org/metadata/{ItemIdentifier}";
    private const string DownloadBaseUrl = $"https://archive.org/download/{ItemIdentifier}/";

    private readonly HttpClient _httpClient;
    private readonly QuranDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly ILogger<ArchiveOrgInventoryService> _logger;

    private static readonly Regex SurahPatternRegex = new(@"سورة\s*([0-9]{1,3})\s*(.*?)(\.mp3|$)", RegexOptions.Compiled);

    public ArchiveOrgInventoryService(
        HttpClient httpClient,
        QuranDbContext dbContext,
        IStorageService storageService,
        ILogger<ArchiveOrgInventoryService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ArchiveSourceItemDto>> FetchRemoteMetadataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching metadata from Archive.org item {Identifier}", ItemIdentifier);

        var response = await _httpClient.GetAsync(MetadataUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var metadataResponse = JsonSerializer.Deserialize<ArchiveMetadataResponse>(json);

        if (metadataResponse?.Files == null || metadataResponse.Files.Count == 0)
        {
            _logger.LogWarning("No files found in Archive.org metadata for {Identifier}", ItemIdentifier);
            return Array.Empty<ArchiveSourceItemDto>();
        }

        var result = new List<ArchiveSourceItemDto>();

        foreach (var file in metadataResponse.Files)
        {
            if (file.Source != "original" || !file.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = SurahPatternRegex.Match(file.Name);
            if (!match.Success)
            {
                _logger.LogWarning("Could not parse surah number from filename: {FileName}", file.Name);
                continue;
            }

            var surahNumStr = match.Groups[1].Value;
            if (!int.TryParse(surahNumStr, out var surahNumber))
                continue;

            var surahNameArabic = match.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(surahNameArabic))
            {
                surahNameArabic = $"سورة {surahNumber}";
            }

            var downloadUrl = DownloadBaseUrl + Uri.EscapeDataString(file.Name);
            long.TryParse(file.Size, out var sizeBytes);
            double.TryParse(file.Length, NumberStyles.Any, CultureInfo.InvariantCulture, out var durationSeconds);

            var localPath = GetLocalSourceFilePath(surahNumber);
            var isDownloaded = File.Exists(localPath) && new FileInfo(localPath).Length > 0;
            var localStatus = isDownloaded ? "Downloaded" : "Missing";

            result.Add(new ArchiveSourceItemDto(
                SurahNumber: surahNumber,
                SurahNameArabic: surahNameArabic,
                FileName: file.Name,
                DownloadUrl: downloadUrl,
                SizeBytes: sizeBytes,
                DurationSeconds: durationSeconds,
                Md5: file.Md5,
                Sha1: file.Sha1,
                LocalStatus: localStatus,
                IsDownloaded: isDownloaded,
                LocalFilePath: isDownloaded ? localPath : null
            ));
        }

        var sortedResult = result.OrderBy(x => x.SurahNumber).ToList();
        _logger.LogInformation("Fetched {Count} MP3 original surah files from Archive.org", sortedResult.Count);
        return sortedResult;
    }

    public async Task<IReadOnlyList<ArchiveSourceItemDto>> GetLocalInventoryStatusAsync(CancellationToken cancellationToken = default)
    {
        // 1. Essaye de charger depuis le fichier de cache de référence si disponible
        var inventoryPath = GetInventoryJsonPath();
        List<ArchiveSourceItemDto> remoteList;

        if (File.Exists(inventoryPath))
        {
            var json = await File.ReadAllTextAsync(inventoryPath, cancellationToken);
            remoteList = JsonSerializer.Deserialize<List<ArchiveSourceItemDto>>(json) ?? new();
        }
        else
        {
            remoteList = (await FetchRemoteMetadataAsync(cancellationToken)).ToList();
        }

        var updatedList = new List<ArchiveSourceItemDto>();

        foreach (var item in remoteList)
        {
            var localPath = GetLocalSourceFilePath(item.SurahNumber);
            var exists = File.Exists(localPath);
            var isDownloaded = exists && new FileInfo(localPath).Length > 0;
            var status = isDownloaded ? "Downloaded" : "Missing";

            updatedList.Add(item with
            {
                IsDownloaded = isDownloaded,
                LocalStatus = status,
                LocalFilePath = isDownloaded ? localPath : null
            });
        }

        return updatedList.OrderBy(x => x.SurahNumber).ToList();
    }

    public async Task<ArchiveInventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var items = await GetLocalInventoryStatusAsync(cancellationToken);

        int totalExpected = 114;
        int remoteFound = items.Count;
        int downloaded = items.Count(x => x.IsDownloaded);
        long totalRemoteSize = items.Sum(x => x.SizeBytes);
        long totalDownloadedSize = items
            .Where(x => x.IsDownloaded && !string.IsNullOrEmpty(x.LocalFilePath))
            .Sum(x => new FileInfo(x.LocalFilePath!).Length);

        return new ArchiveInventorySummaryDto(
            TotalExpected: totalExpected,
            TotalRemoteFound: remoteFound,
            TotalDownloaded: downloaded,
            TotalRemoteSizeBytes: totalRemoteSize,
            TotalDownloadedSizeBytes: totalDownloadedSize,
            IsFullySynchronized: downloaded >= totalExpected && totalExpected == remoteFound
        );
    }

    public async Task<int> SyncInventoryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Synchronizing Archive.org inventory to local reference and database");
        var items = await FetchRemoteMetadataAsync(cancellationToken);

        if (items.Count == 0)
        {
            _logger.LogWarning("No items to sync.");
            return 0;
        }

        // 1. Sauvegarder dans data/reference/archive_org_114_inventory.json
        var inventoryPath = GetInventoryJsonPath();
        Directory.CreateDirectory(Path.GetDirectoryName(inventoryPath)!);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(items, jsonOptions);
        await File.WriteAllTextAsync(inventoryPath, json, cancellationToken);
        _logger.LogInformation("Saved inventory to {Path}", inventoryPath);

        // 2. Mettre à jour la table Sources en base de données
        var existingSource = await _dbContext.Sources.FirstOrDefaultAsync(s => s.Type == "ArchiveOrg" && s.Name.Contains(ItemIdentifier), cancellationToken);
        if (existingSource == null)
        {
            existingSource = new Source
            {
                Name = $"Archive.org ({ItemIdentifier}) - صابر عبد الحكم مصحف ورش",
                Type = "ArchiveOrg",
                Url = $"https://archive.org/details/{ItemIdentifier}",
                Description = "المصحف المرتل برواية ورش عن نافع بجميع الأوجه الشاطبية - 114 سورة كاملة"
            };
            _dbContext.Sources.Add(existingSource);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return items.Count;
    }

    public async Task<bool> DownloadSurahSourceAsync(int surahNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting download for Surah #{Number}", surahNumber);

        var items = await GetLocalInventoryStatusAsync(cancellationToken);
        var item = items.FirstOrDefault(x => x.SurahNumber == surahNumber);

        if (item == null)
        {
            _logger.LogError("Surah #{Number} not found in inventory", surahNumber);
            return false;
        }

        var targetFilePath = GetLocalSourceFilePath(surahNumber);
        var tempFilePath = targetFilePath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

        try
        {
            using var response = await _httpClient.GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await contentStream.CopyToAsync(fileStream, cancellationToken);
            }

            // Vérification de la taille minimale
            var fileInfo = new FileInfo(tempFilePath);
            if (fileInfo.Length == 0)
            {
                _logger.LogError("Downloaded file for Surah #{Number} is empty", surahNumber);
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                return false;
            }

            // Vérification MD5 si disponible
            if (!string.IsNullOrEmpty(item.Md5))
            {
                var computedMd5 = await ComputeMd5Async(tempFilePath, cancellationToken);
                if (!string.Equals(computedMd5, item.Md5, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("MD5 mismatch for Surah #{Number}. Expected {Exp}, Got {Got}", surahNumber, item.Md5, computedMd5);
                }
            }

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            File.Move(tempFilePath, targetFilePath);
            _logger.LogInformation("Successfully downloaded and saved Surah #{Number} to {Path} ({Size} bytes)", surahNumber, targetFilePath, fileInfo.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download Surah #{Number} from {Url}", surahNumber, item.DownloadUrl);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            return false;
        }
    }

    public async Task<int> DownloadBatchAsync(IEnumerable<int> surahNumbers, CancellationToken cancellationToken = default)
    {
        int successfulDownloads = 0;
        foreach (var surahNumber in surahNumbers)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var success = await DownloadSurahSourceAsync(surahNumber, cancellationToken);
            if (success) successfulDownloads++;
        }
        return successfulDownloads;
    }

    private string GetLocalSourceFilePath(int surahNumber)
    {
        var relative = Path.Combine("sources", $"{surahNumber:D3}", "original.mp3");
        return _storageService.GetPhysicalPath(relative);
    }

    private string GetInventoryJsonPath()
    {
        var candidate1 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "reference", "archive_org_114_inventory.json");
        if (Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(candidate1))))
            return Path.GetFullPath(candidate1);

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "reference", "archive_org_114_inventory.json"));
    }

    private static async Task<string> ComputeMd5Async(string filePath, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private record ArchiveMetadataResponse(
        [property: JsonPropertyName("files")] List<ArchiveFileEntry>? Files
    );

    private record ArchiveFileEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("size")] string? Size,
        [property: JsonPropertyName("length")] string? Length,
        [property: JsonPropertyName("md5")] string? Md5,
        [property: JsonPropertyName("sha1")] string? Sha1
    );
}
