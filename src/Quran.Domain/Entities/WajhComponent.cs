namespace Quran.Domain.Entities;

public class WajhComponent
{
    public int Id { get; set; }
    public int WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public string ComponentKey { get; set; } = string.Empty; // ex: "badal", "linMahmuz", "yaSin", "nunBeforeWaw", "maUndhira", "abaahum"
    public string ValueArabic { get; set; } = string.Empty; // ex: "فتح", "إدغام", "قصر", "توسط", "إشباع", "فويق القصر"
    public int? ValueNumeric { get; set; } // ex: 2, 4, 6
}
