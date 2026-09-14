using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class Surah
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string NameArabic { get; set; } = string.Empty;
    public string NameLatin { get; set; } = string.Empty;
    public string? NameTranslation { get; set; }
    public int AyahCount { get; set; }
    public int RevelationOrder { get; set; }
    public RevelationType RevelationType { get; set; } = RevelationType.Meccan;
    public bool IsActive { get; set; } = true;

    public ICollection<Ayah> Ayahs { get; set; } = new List<Ayah>();
}
