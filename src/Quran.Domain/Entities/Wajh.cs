namespace Quran.Domain.Entities;

public class Wajh
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: "w01", "taqlil", "badal_tawassut", "az-sh-badal2-lin4"
    public string NameArabic { get; set; } = string.Empty; // ex: "الوجه 1", "توسط البدل مع التقليل"
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string RouteCode { get; set; } = "";
    public int? TransmissionRouteId { get; set; }
    public TransmissionRoute? TransmissionRoute { get; set; }

    public int? ReadingRuleId { get; set; }
    public ReadingRule? ReadingRule { get; set; }

    public string Category { get; set; } = "Tahrirat"; // "Usul", "Farsh", "Tahrirat"
    public string? RuleGroup { get; set; } // "badal-lin", "yasin-opening"
    public string? ExcludedScope { get; set; }
    public int? SourceBranch { get; set; } // for branch numbered studies (e.g. 1..10 in Yasin)
    public string? SourceBooks { get; set; } // JSON or text list of source books

    public string ReviewStatus { get; set; } = "PendingReview"; // "Draft", "PendingReview", "Approved", "Rejected", "Archived"
    public string? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public string SourceCitation { get; set; } = "";
    public string DefinitionJson { get; set; } = "{}";
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Recitation> Recitations { get; set; } = new List<Recitation>();

    public ICollection<WajhComponent> Components { get; set; } = new List<WajhComponent>();
    public ICollection<WajhApplicationScope> Scopes { get; set; } = new List<WajhApplicationScope>();
    public ICollection<EvidenceCitation> Citations { get; set; } = new List<EvidenceCitation>();
    public ICollection<WajhRevision> Revisions { get; set; } = new List<WajhRevision>();
}
