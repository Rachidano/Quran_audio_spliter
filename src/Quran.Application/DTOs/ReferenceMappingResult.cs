namespace Quran.Application.DTOs;

public class ReferenceMappingResult
{
    public bool IsMapped { get; set; }
    public string SourceCorpusCode { get; set; } = null!;
    public string SourceReference { get; set; } = null!;
    public string TargetCorpusCode { get; set; } = null!;
    public string? TargetReference { get; set; }
    public string Cardinality { get; set; } = "Unmapped";
    public string? DifferenceDescription { get; set; }
    public string ReviewStatus { get; set; } = "PendingReview";
    public string? ProofNotes { get; set; }
}
