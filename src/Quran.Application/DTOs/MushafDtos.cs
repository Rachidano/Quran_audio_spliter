namespace Quran.Application.DTOs;

public record MushafEditionDto(
    int Id,
    string Code,
    string NameArabic,
    string? NameLatin,
    int RiwayaId,
    string? Publisher,
    string? SourceUrl,
    bool IsActive
);

public record MushafPageDto(
    int Id,
    int MushafEditionId,
    int PageNumber,
    string ImageUrl,
    string MasterStorageKey,
    string WebStorageKey,
    int Width,
    int Height,
    string? ChecksumSha256
);

public record AyahImageRegionDto(
    long Id,
    long AyahId,
    int AyahNumber,
    int SurahNumber,
    string SurahNameArabic,
    int MushafPageId,
    int PageNumber,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    int Sequence,
    int Status
);

public record AudioSegmentImageRegionDto(
    long Id,
    Guid AudioSegmentId,
    int MushafPageId,
    int PageNumber,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    int Sequence
);

public record CreateAyahRegionRequest(
    long AyahId,
    int MushafPageId,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    int Sequence = 1,
    int Status = 1
);

public record UpdateAyahRegionRequest(
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    int Sequence,
    int Status
);

public record CreateSegmentImageRegionRequest(
    Guid AudioSegmentId,
    int MushafPageId,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    int Sequence = 1
);

public record MushafPageViewerDto(
    MushafPageDto Page,
    List<AyahImageRegionDto> AyahRegions,
    List<AyahSummaryDto> Ayahs
);

public record AyahSummaryDto(
    long Id,
    int SurahNumber,
    string SurahNameArabic,
    int AyahNumber,
    int NumberInQuran,
    string TextWarsh,
    int? PageNumber
);
