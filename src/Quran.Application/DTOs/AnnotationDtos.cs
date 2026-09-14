using Quran.Domain.Enums;

namespace Quran.Application.DTOs;

public record SaveOccurrenceRequest(
    Guid? Id,
    Guid SourceAudioId,
    long StartMs,
    long EndMs,
    string? Reference,
    long? AyahId,
    int? WajhId,
    AudioSegmentType SegmentType,
    Guid? RepeatOfId,
    string? Notes,
    string? Edition,
    string? SourceHash,
    string? ObservationJson,
    int? ClientVersion
);

public record CaptureEndAyahRequest(
    Guid SourceAudioId,
    long CapturedTimeMs,
    long CurrentStartMs,
    string CurrentReference,
    long? CurrentAyahId,
    int? WajhId,
    string? Notes,
    string? Edition,
    string? SourceHash,
    string? IdempotencyKey
);

public record CaptureEndAyahResult(
    AudioSegmentDto SavedOccurrence,
    long NextStartMs,
    string? NextReference,
    int NextSurahNumber,
    int NextAyahNumber,
    bool HasMoreAyahs
);

public record CaptureRepeatRequest(
    Guid SourceAudioId,
    Guid TargetSegmentId,
    long StartMs,
    long EndMs,
    int? WajhId,
    string? Notes,
    string? Edition,
    string? SourceHash
);

public record AnnotationSessionDto(
    Guid Id,
    string UserId,
    Guid SourceAudioId,
    long CurrentTimeMs,
    int CurrentSurahNumber,
    int CurrentAyahNumber,
    Guid? LastSegmentId,
    bool IsRepeatMode,
    Guid? RepeatTargetSegmentId,
    DateTimeOffset UpdatedAt
);

public record UpdateSessionRequest(
    Guid SourceAudioId,
    long CurrentTimeMs,
    int CurrentSurahNumber,
    int CurrentAyahNumber,
    Guid? LastSegmentId,
    bool IsRepeatMode,
    Guid? RepeatTargetSegmentId
);

public record OccurrenceMigrationReport(
    int MigratedCount,
    int ExistingPreservedCount,
    List<string> Details,
    List<string> Errors
);

public record UndoCaptureResult(
    bool Success,
    Guid RemovedSegmentId,
    long RestoredTimeMs,
    int RestoredSurahNumber,
    int RestoredAyahNumber,
    string? RestoredReference,
    string Message
);

