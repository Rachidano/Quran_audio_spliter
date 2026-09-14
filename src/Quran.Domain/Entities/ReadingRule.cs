namespace Quran.Domain.Entities;

public class ReadingRule
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: "badal-lin", "yasin-opening"
    public string NameArabic { get; set; } = string.Empty; // ex: "اجتماع البدل واللين المهموز"
    public string? Description { get; set; }
    public string Category { get; set; } = "Tahrirat"; // "Usul", "Farsh", "Tahrirat"

    public ICollection<ReadingRealization> Realizations { get; set; } = new List<ReadingRealization>();
    public ICollection<Wajh> Awjuh { get; set; } = new List<Wajh>();
}
