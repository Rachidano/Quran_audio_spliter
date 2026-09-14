namespace Quran.Domain.Entities;

public class ReferenceMapping
{
    public long Id { get; set; }

    public int SourceCorpusId { get; set; }
    public QuranCorpus? SourceCorpus { get; set; }
    public string SourceReference { get; set; } = null!;

    public int TargetCorpusId { get; set; }
    public QuranCorpus? TargetCorpus { get; set; }
    public string TargetReference { get; set; } = null!;

    public int SurahNumber { get; set; }

    public string Cardinality { get; set; } = "OneToOne";
    public string? DifferenceDescription { get; set; }
    public string ReviewStatus { get; set; } = "PendingReview";
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ProofNotes { get; set; }
}
