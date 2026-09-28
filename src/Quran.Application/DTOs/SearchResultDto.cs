namespace Quran.Application.DTOs;

public record SearchResultDto(
    int SurahNumber,
    string SurahNameArabic,
    string SurahNameLatin,
    int AyahNumber,
    string TextWarsh,
    string MatchedText,
    int? PageNumber = null,
    bool IsProvisional = false
);
