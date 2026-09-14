namespace Quran.Domain.Entities;

public class EvidenceCitation
{
    public int Id { get; set; }
    public int ReferenceSourceId { get; set; }
    public ReferenceSource? ReferenceSource { get; set; }

    public int? WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public int? CombinationConstraintId { get; set; }
    public CombinationConstraint? CombinationConstraint { get; set; }

    public string? Locator { get; set; } // specific page/folio/locator
    public string? QuoteOrNote { get; set; }
}
