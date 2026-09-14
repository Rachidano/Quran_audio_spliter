namespace Quran.Domain.Entities;

public class AudioSegmentImageRegion
{
    public long Id { get; set; }
    public Guid AudioSegmentId { get; set; }
    public AudioSegment? AudioSegment { get; set; }

    public int MushafPageId { get; set; }
    public MushafPage? MushafPage { get; set; }

    // Normalized coordinates between 0.0 and 1.0
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }

    public int Sequence { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
