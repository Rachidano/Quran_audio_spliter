namespace Quran.Domain.Entities;

public class WajhApplicationScope
{
    public int Id { get; set; }
    public int WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public int? SourceSurah { get; set; } // ex: 36
    public int? SourceAyahStart { get; set; } // ex: 1
    public int? SourceAyahEnd { get; set; } // ex: 6
    public string MarwMappingStatus { get; set; } = "NotMapped"; // "NotMapped", "Mapped", "PendingReview"
    public bool AutomaticAssignment { get; set; } = false;
    public string? ContextDescription { get; set; }
}
