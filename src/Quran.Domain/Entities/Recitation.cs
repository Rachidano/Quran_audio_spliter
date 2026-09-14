using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class Recitation
{
    public long Id { get; set; }

    public long AyahId { get; set; }
    public Ayah Ayah { get; set; } = null!;

    public int ReciterId { get; set; }
    public Reciter Reciter { get; set; } = null!;

    public int RiwayaId { get; set; }
    public Riwaya Riwaya { get; set; } = null!;

    public int? WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public long AudioAssetId { get; set; }
    public AudioAsset AudioAsset { get; set; } = null!;

    public RecitationStatus Status { get; set; } = RecitationStatus.Imported;
    public bool IsPublished { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<ValidationReview> ValidationReviews { get; set; } = new List<ValidationReview>();
}
