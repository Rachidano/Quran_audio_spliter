using Quran.Application.DTOs;
using Quran.Domain.Enums;

namespace Quran.Application.Interfaces;

public interface IAudioEditorService
{
    Task<AudioEditorContextDto> GetEditorContextBySurahAsync(int surahNumber, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> CreateSegmentAsync(CreateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> UpdateSegmentAsync(Guid id, UpdateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default);
    Task DeleteSegmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> SubmitForReviewAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> ApproveSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> RejectSegmentAsync(Guid id, string reason, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> RenderSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> PublishSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> UnpublishSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioSegmentDto>> GetReviewQueueAsync(int? surahNumber = null, AudioSegmentStatus? status = null, CancellationToken cancellationToken = default);
    Task<int> TokenizeAyahsAsync(int surahNumber, CancellationToken cancellationToken = default);
}
