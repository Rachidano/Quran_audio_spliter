using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IAudioEditorService
{
    Task<AudioEditorContextDto> GetEditorContextBySurahAsync(int surahNumber, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> CreateSegmentAsync(CreateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> UpdateSegmentAsync(Guid id, UpdateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default);
    Task DeleteSegmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> ApproveSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> RenderSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<AudioSegmentDto> PublishSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<int> TokenizeAyahsAsync(int surahNumber, CancellationToken cancellationToken = default);
}
