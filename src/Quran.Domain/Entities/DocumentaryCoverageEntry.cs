namespace Quran.Domain.Entities;

public class DocumentaryCoverageEntry
{
    public int Id { get; set; }
    public string TransmissionRouteCode { get; set; } = string.Empty; // "azraq-shatibiyya", "asbahani-tayyiba", "azraq-tayyiba"
    public string SourceWorkTitle { get; set; } = string.Empty; // "الشاطبية", "طيبة النشر", "تنقيح كتاب التحرير", "دراسة اجتماع البدل واللين"
    public string ChapterOrSection { get; set; } = string.Empty; // "سورة البقرة", "سورة يس", "باب المد والقصر", "باب الإدغام الكبير"
    public int? SurahNumber { get; set; }
    public string ScopeContext { get; set; } = string.Empty; // "اجتماع البدل واللين المهموز", "فواتح يس 1-6", "نون والقلم"
    public string AnalysisStatus { get; set; } = "NonEtudiee"; // "NonEtudiee", "Extraite", "AResoudre", "Revue", "HorsPerimetre"
    public int EntriesCount { get; set; } = 0;
    public int ConstraintsCount { get; set; } = 0;
    public string? Notes { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
}
