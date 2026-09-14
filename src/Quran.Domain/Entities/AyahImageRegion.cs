namespace Quran.Domain.Entities;

public class AyahImageRegion
{
    public long Id { get; set; }
    public long AyahId { get; set; }
    public Ayah? Ayah { get; set; }

    public int MushafPageId { get; set; }
    public MushafPage? MushafPage { get; set; }

    // Normalized coordinates between 0.0 and 1.0
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }

    public int Sequence { get; set; } = 1;
    public int Status { get; set; } = 1; // 1=Draft, 2=Reviewed, 3=Approved
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
