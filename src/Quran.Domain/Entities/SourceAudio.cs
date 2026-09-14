using Quran.Domain.Enums;

namespace Quran.Domain.Entities;

public class SourceAudio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int SurahId { get; set; }
    public Surah Surah { get; set; } = null!;

    public int ReciterId { get; set; }
    public Reciter Reciter { get; set; } = null!;

    public int RiwayaId { get; set; }
    public Riwaya Riwaya { get; set; } = null!;

    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty; // ex: "sources/002/original.mp3"
    public string? OriginalUrl { get; set; }

    public long DurationMs { get; set; }
    public long SizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public int? Bitrate { get; set; }
    public string? Codec { get; set; } = "mp3";

    public SourceAudioStatus Status { get; set; } = SourceAudioStatus.Available;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AudioSegment> AudioSegments { get; set; } = new List<AudioSegment>();
}
