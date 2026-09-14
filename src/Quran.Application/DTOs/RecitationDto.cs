namespace Quran.Application.DTOs;

public record RecitationDto(
    long Id,
    string ReciterNameArabic,
    string ReciterNameLatin,
    string RiwayaNameArabic,
    string? WajhCode,
    string? WajhNameArabic,
    string AudioUrl,
    long DurationMs,
    long SizeBytes,
    string Status,
    bool IsPublished
);
