using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class SegmentRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AudioSegmentId { get; set; }
    public AudioSegment? AudioSegment { get; set; }

    public int RevisionNumber { get; set; } = 1;
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string? Reference { get; set; }
    public AudioSegmentType SegmentType { get; set; } = AudioSegmentType.FullAyah;

    public int? WajhId { get; set; }
    public Wajh? Wajh { get; set; }
    public string? WajhAssignmentStatus { get; set; }
    public string? ProofNotes { get; set; }

    public string CreatedByUserId { get; set; } = "admin";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
