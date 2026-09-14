namespace Quran.Domain.Entities;

public class MushafEdition
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameArabic { get; set; } = null!;
    public string? NameLatin { get; set; }
    public int RiwayaId { get; set; }
    public Riwaya? Riwaya { get; set; }
    public string? Publisher { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MushafPage> Pages { get; set; } = new List<MushafPage>();
}
