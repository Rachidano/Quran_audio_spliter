using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IAnnotationService
{
    Task<AudioSegmentDto> SaveOccurrenceAsync(SaveOccurrenceRequest request, string userId, CancellationToken ct = default);
    Task<CaptureEndAyahResult> CaptureEndAyahAsync(CaptureEndAyahRequest request, string userId, CancellationToken ct = default);
    Task<AudioSegmentDto> CaptureRepeatAsync(CaptureRepeatRequest request, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<AudioSegmentDto>> GetOccurrencesForSourceAsync(Guid sourceAudioId, CancellationToken ct = default);
    Task<IReadOnlyList<AudioSegmentDto>> GetOccurrencesForReferenceAsync(string edition, string reference, bool publishedOnly, CancellationToken ct = default);
    Task<AnnotationSessionDto> GetOrCreateSessionAsync(string userId, Guid sourceAudioId, CancellationToken ct = default);
    Task<AnnotationSessionDto> UpdateSessionAsync(UpdateSessionRequest request, string userId, CancellationToken ct = default);
    Task<OccurrenceMigrationReport> MigrateMushafPassagesToAudioSegmentsAsync(CancellationToken ct = default);
    Task<bool> DeleteOccurrenceAsync(Guid id, int clientVersion, string userId, CancellationToken ct = default);
}
