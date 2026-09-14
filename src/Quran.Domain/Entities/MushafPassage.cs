namespace Quran.Domain.Entities;

// Edition-qualified reference: deliberately independent of legacy Ayah IDs.
public class MushafPassage
{
    public Guid Id { get; set; }
    public string Edition { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string? Reference { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public int SegmentType { get; set; }
    public Guid? RepeatOfId { get; set; }
    public int? WajhId { get; set; }
    public Wajh? Wajh { get; set; }
    public string Notes { get; set; } = "";
    public string ObservationJson { get; set; } = "{}";
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
