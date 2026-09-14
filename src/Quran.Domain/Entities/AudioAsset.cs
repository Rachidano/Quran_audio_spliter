namespace Quran.Domain.Entities;

public class AudioAsset
{
    public long Id { get; set; }
    public string StorageKey { get; set; } = string.Empty; // ex: "published/001/001/warsh/w01.mp3"
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "audio/mpeg";
    public long DurationMs { get; set; }
    public long SizeBytes { get; set; }
    public int Bitrate { get; set; } // in kbps, ex: 128
    public string Checksum { get; set; } = string.Empty; // SHA-256

    public int? SourceId { get; set; }
    public Source? Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Recitation> Recitations { get; set; } = new List<Recitation>();
}
