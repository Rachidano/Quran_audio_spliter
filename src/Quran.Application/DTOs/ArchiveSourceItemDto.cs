namespace Quran.Application.DTOs;

public record ArchiveSourceItemDto(
    int SurahNumber,
    string SurahNameArabic,
    string FileName,
    string DownloadUrl,
    long SizeBytes,
    double DurationSeconds,
    string? Md5,
    string? Sha1,
    string LocalStatus, // "Missing", "Downloaded", "Verified", "Error"
    bool IsDownloaded,
    string? LocalFilePath
);

public record ArchiveInventorySummaryDto(
    int TotalExpected,
    int TotalRemoteFound,
    int TotalDownloaded,
    long TotalRemoteSizeBytes,
    long TotalDownloadedSizeBytes,
    bool IsFullySynchronized
);
