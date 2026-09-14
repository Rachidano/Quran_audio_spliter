namespace Quran.Domain.Entities;

public class MushafPage
{
    public int Id { get; set; }
    public int MushafEditionId { get; set; }
    public MushafEdition? MushafEdition { get; set; }

    public int PageNumber { get; set; }
    public string MasterStorageKey { get; set; } = null!;
    public string WebStorageKey { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public string? ChecksumSha256 { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AyahImageRegion> AyahImageRegions { get; set; } = new List<AyahImageRegion>();
    public ICollection<AudioSegmentImageRegion> SegmentImageRegions { get; set; } = new List<AudioSegmentImageRegion>();
}
