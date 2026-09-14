namespace Quran.Domain.Entities;

public class ReferenceSource
{
    public int Id { get; set; }
    public string SourceCode { get; set; } = string.Empty; // ex: "badal-lin-study", "asbahani-tanqih-p185"
    public string Title { get; set; } = string.Empty; // ex: "تنقيح كتاب التحرير — جدول أول يس"
    public string? Author { get; set; }
    public string? Locator { get; set; } // ex: "ص 185 المطبوعة، صفحة PDF الخامسة، الفروع 1–10"
    public string? Url { get; set; }
    public string? Sha256 { get; set; }
    public string Kind { get; set; } = "SpecialistStudy"; // "PrimarySource", "SpecialistStudy", "SecondaryStudy"

    public ICollection<EvidenceCitation> Citations { get; set; } = new List<EvidenceCitation>();
}
