namespace Quran.Domain.Entities;

public class CombinationConstraint
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: "as-haantum-no-qasr", "as-yasin-izhar"
    public int? TransmissionRouteId { get; set; }
    public TransmissionRoute? TransmissionRoute { get; set; }
    public string RouteCode { get; set; } = string.Empty; // ex: "asbahani-tayyiba"
    public string NameArabic { get; set; } = string.Empty; // ex: "قيد هاأنتم"
    public string ContextArabic { get; set; } = string.Empty; // ex: "إثبات ألف هاأنتم ومدها مع تسهيل الهمزة"
    public string ConditionJson { get; set; } = "{}";
    public string EffectJson { get; set; } = "{}";
    public string ExplanationArabic { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "PendingReview"; // "Draft", "PendingReview", "Approved", "Archived"
    public string? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public bool AutomaticEnforcement { get; set; } = false;
    public string MarwMappingStatus { get; set; } = "NotMapped";

    public ICollection<EvidenceCitation> Citations { get; set; } = new List<EvidenceCitation>();
}
