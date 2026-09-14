namespace Quran.Domain.Entities;

public class Ayah
{
    public long Id { get; set; }
    public int SurahId { get; set; }
    public Surah Surah { get; set; } = null!;

    public int AyahNumber { get; set; }
    public int NumberInQuran { get; set; }

    public string TextWarsh { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;

    public int? PageNumber { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }

    public ICollection<Recitation> Recitations { get; set; } = new List<Recitation>();
    public ICollection<AyahWord> Words { get; set; } = new List<AyahWord>();
    public ICollection<AudioSegment> AudioSegments { get; set; } = new List<AudioSegment>();
    public ICollection<AyahImageRegion> ImageRegions { get; set; } = new List<AyahImageRegion>();
}
