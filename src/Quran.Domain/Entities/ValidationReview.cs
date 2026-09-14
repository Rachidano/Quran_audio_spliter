using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class ValidationReview
{
    public long Id { get; set; }

    public long RecitationId { get; set; }
    public Recitation Recitation { get; set; } = null!;

    public string? ValidatorUserId { get; set; }
    public RecitationStatus Status { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.UtcNow;
}
