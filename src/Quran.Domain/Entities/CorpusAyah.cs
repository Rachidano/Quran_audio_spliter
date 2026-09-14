namespace Quran.Domain.Entities;

public class CorpusAyah
{
    public long Id { get; set; }
    public int CorpusId { get; set; }
    public QuranCorpus? Corpus { get; set; }

    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public int NumberInQuran { get; set; }

    public string TextArabic { get; set; } = null!;
    public string SearchText { get; set; } = null!;

    public int PageNumberHint { get; set; }
}
