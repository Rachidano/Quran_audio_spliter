namespace Quran.Domain.Entities;

public class QuranCorpus
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameArabic { get; set; } = null!;
    public string? NameLatin { get; set; }
    public string RiwayaCode { get; set; } = "warsh";
    public string CountingSystem { get; set; } = null!;
    public int TotalAyahs { get; set; }
    public string TextualRevision { get; set; } = "1.0";
    public string Provenance { get; set; } = null!;
    public string QualificationStatus { get; set; } = "Unqualified";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CorpusAyah> Ayahs { get; set; } = new List<CorpusAyah>();
    public ICollection<MushafEdition> Editions { get; set; } = new List<MushafEdition>();
}
