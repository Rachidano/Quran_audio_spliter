namespace Quran.Domain.Entities;

public class Riwaya
{
    public int Id { get; set; }
    public string NameArabic { get; set; } = string.Empty;
    public string NameLatin { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Recitation> Recitations { get; set; } = new List<Recitation>();
}
