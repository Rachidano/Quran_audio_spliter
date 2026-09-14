namespace Quran.Domain.Entities;

public class SegmentTextSpan
{
    public long Id { get; set; }

    public Guid AudioSegmentId { get; set; }
    public AudioSegment? AudioSegment { get; set; }

    public string CorpusCode { get; set; } = "marw-newMushaf";
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }

    public int? StartWordIndex { get; set; }
    public int? EndWordIndex { get; set; }

    public int Order { get; set; } = 1;
}
