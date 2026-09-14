using Quran.Domain.Enums;

namespace Quran.Application.DTOs;

public record SourceAudioDto(
    Guid Id,
    int SurahId,
    int SurahNumber,
    string SurahNameArabic,
    int ReciterId,
    string ReciterName,
    int RiwayaId,
    string RiwayaName,
    string StorageKey,
    string AudioUrl,
    long DurationMs,
    long SizeBytes,
    SourceAudioStatus Status
);

public record AyahWordDto(
    long Id,
    long AyahId,
    int WordIndex,
    string TextWarsh,
    string? SearchText
);

public record AyahEditorDto(
    long Id,
    int SurahId,
    int AyahNumber,
    int NumberInQuran,
    string TextWarsh,
    List<AyahWordDto> Words
);

public record WajhOptionDto(
    int Id,
    string Code,
    string NameArabic,
    string? Description
);

public record AudioSegmentDto(
    Guid Id,
    Guid SourceAudioId,
    long? AyahId,
    int? AyahNumber,
    int? WajhId,
    string? WajhCode,
    string? WajhNameArabic,
    long StartMs,
    long EndMs,
    int? StartWordIndex,
    int? EndWordIndex,
    AudioSegmentType SegmentType,
    int SequenceNumber,
    AudioSegmentStatus Status,
    string? Notes,
    string? PublishedUrl,
    string RowVersionBase64,
    DateTimeOffset CreatedAt
);

public record AudioEditorContextDto(
    SourceAudioDto SourceAudio,
    int SurahNumber,
    string SurahNameArabic,
    string SurahNameLatin,
    int AyahCount,
    List<AyahEditorDto> Ayahs,
    List<WajhOptionDto> Awjuh,
    List<AudioSegmentDto> Segments
);

public record CreateAudioSegmentRequest(
    Guid SourceAudioId,
    long? AyahId,
    int? WajhId,
    long StartMs,
    long EndMs,
    int? StartWordIndex,
    int? EndWordIndex,
    AudioSegmentType SegmentType,
    string? Notes
);

public record UpdateAudioSegmentRequest(
    long? AyahId,
    int? WajhId,
    long StartMs,
    long EndMs,
    int? StartWordIndex,
    int? EndWordIndex,
    AudioSegmentType SegmentType,
    string? Notes,
    string RowVersionBase64
);
