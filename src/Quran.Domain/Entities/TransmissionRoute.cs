namespace Quran.Domain.Entities;

public class TransmissionRoute
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: "azraq-shatibiyya", "azraq-tayyiba", "asbahani-tayyiba"
    public string NameArabic { get; set; } = string.Empty; // ex: "الأزرق — الشاطبية"
    public string Riwaya { get; set; } = "ورش عن نافع";
    public string Tariq { get; set; } = string.Empty; // ex: "الأزرق", "الأصبهاني"
    public string? SubTariq { get; set; } // ex: "طريق الشاطبية", "طرق طيبة النشر"
    public string? Description { get; set; }
    public string CoverageStatus { get; set; } = "Partial"; // "Partial", "NotDocumented", "Exhaustive", "PendingResearch"
    public int? ParentRouteId { get; set; }
    public TransmissionRoute? ParentRoute { get; set; }

    public ICollection<TransmissionRoute> SubRoutes { get; set; } = new List<TransmissionRoute>();
    public ICollection<Wajh> Awjuh { get; set; } = new List<Wajh>();
    public ICollection<CombinationConstraint> Constraints { get; set; } = new List<CombinationConstraint>();
}
