namespace Quran.Domain.Entities;

public class AyahWord
{
    public long Id { get; set; }
    public long AyahId { get; set; }
    public Ayah Ayah { get; set; } = null!;

    public int WordIndex { get; set; } // 1-based index du mot dans l'ayah
    public string TextWarsh { get; set; } = string.Empty;
    public string? SearchText { get; set; }
}
