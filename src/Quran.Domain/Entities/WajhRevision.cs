namespace Quran.Domain.Entities;

public class WajhRevision
{
    public int Id { get; set; }
    public int WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public int RevisionNumber { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string RouteCode { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string DefinitionSnapshotJson { get; set; } = "{}";
    public string? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ChangeReason { get; set; }
    public bool AffectsSemanticRecitation { get; set; } = true;
}
