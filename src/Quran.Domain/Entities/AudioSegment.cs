using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class AudioSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceAudioId { get; set; }
    public SourceAudio SourceAudio { get; set; } = null!;

    public long AyahId { get; set; }
    public Ayah Ayah { get; set; } = null!;

    public int? WajhId { get; set; }
    public Wajh? Wajh { get; set; }

    public long StartMs { get; set; } // En millisecondes (BigInt)
    public long EndMs { get; set; }   // En millisecondes (BigInt)

    public int? StartWordIndex { get; set; } // null si toute l'ayah
    public int? EndWordIndex { get; set; }   // null si toute l'ayah

    public AudioSegmentType SegmentType { get; set; } = AudioSegmentType.FullAyah;
    public int SequenceNumber { get; set; } = 1;

    public AudioSegmentStatus Status { get; set; } = AudioSegmentStatus.Draft;
    public string? Notes { get; set; }

    public string CreatedByUserId { get; set; } = "admin";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public string? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }

    public long? PublishedAudioAssetId { get; set; }
    public AudioAsset? PublishedAudioAsset { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // Optimistic concurrency

    public ICollection<AudioSegmentImageRegion> SegmentImageRegions { get; set; } = new List<AudioSegmentImageRegion>();
}
