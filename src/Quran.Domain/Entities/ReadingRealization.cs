namespace Quran.Domain.Entities;

public class ReadingRealization
{
    public int Id { get; set; }
    public int ReadingRuleId { get; set; }
    public ReadingRule? ReadingRule { get; set; }

    public string Code { get; set; } = string.Empty; // ex: "badal-2", "lin-4", "fath", "taqlil"
    public string ValueArabic { get; set; } = string.Empty; // ex: "قصر البدل (حركتان)"
    public int? ValueNumeric { get; set; } // ex: 2, 4, 6
    public string? Description { get; set; }
}
