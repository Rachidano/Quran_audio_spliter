namespace Quran.Domain.Entities;

public class Wajh
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: "w01", "taqlil", "badal_tawassut"
    public string NameArabic { get; set; } = string.Empty; // ex: "الوجه 1", "توسط البدل مع التقليل"
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string RouteCode { get; set; } = "";
    public string ReviewStatus { get; set; } = "PendingReview";
    public string SourceCitation { get; set; } = "";
    public string DefinitionJson { get; set; } = "{}";
    public int Version { get; set; } = 1;

    public ICollection<Recitation> Recitations { get; set; } = new List<Recitation>();
}
