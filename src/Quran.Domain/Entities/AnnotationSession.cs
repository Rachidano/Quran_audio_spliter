namespace Quran.Domain.Entities;

public class AnnotationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = null!;
    public Guid SourceAudioId { get; set; }
    public SourceAudio? SourceAudio { get; set; }

    public long CurrentTimeMs { get; set; }
    public int CurrentSurahNumber { get; set; } = 1;
    public int CurrentAyahNumber { get; set; } = 1;

    public Guid? LastSegmentId { get; set; }
    public AudioSegment? LastSegment { get; set; }

    public bool IsRepeatMode { get; set; }
    public Guid? RepeatTargetSegmentId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
