namespace Quran.Application.DTOs;

public record SurahDto(
    int Number,
    string NameArabic,
    string NameLatin,
    string? NameTranslation,
    int AyahCount,
    int RevelationOrder,
    string RevelationType,
    bool IsActive
);
