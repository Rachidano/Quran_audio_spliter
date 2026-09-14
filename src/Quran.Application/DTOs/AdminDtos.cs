namespace Quran.Application.DTOs;

public record AdminDashboardDto(
    int TotalSurahs,
    int TotalAyahs,
    int AyahsWithAudio,
    int AyahsWithoutAudio,
    int SegmentsPendingValidation,
    int SegmentsPublished,
    int SegmentsRejected
);

public record AdminRecitationItemDto(
    long RecitationId,
    int SurahNumber,
    string SurahNameArabic,
    int AyahNumber,
    string TextWarsh,
    int? WajhId,
    string? WajhCode,
    string? WajhNameArabic,
    string AudioUrl,
    long DurationMs,
    long SizeBytes,
    string Status,
    bool IsPublished,
    DateTimeOffset CreatedAt
);

public record CorpusCheckReport(
    int SurahCount,
    int ExpectedSurahCount,
    int TotalAyahs,
    int AyahsWithoutText,
    int AyahsWithoutAudio,
    int AudioWithoutWajh,
    int MissingFilesCount,
    int ChecksumMismatchCount,
    int UnvalidatedPublishedCount,
    List<string> Issues,
    bool IsCorpusHealthy
);
