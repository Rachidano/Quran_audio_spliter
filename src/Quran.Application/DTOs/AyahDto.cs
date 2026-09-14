namespace Quran.Application.DTOs;

public record AyahDto(
    long Id,
    int SurahNumber,
    string SurahNameArabic,
    int AyahNumber,
    int NumberInQuran,
    string TextWarsh,
    int? PageNumber,
    int? LineStart,
    int? LineEnd,
    List<RecitationDto> Recitations
);
