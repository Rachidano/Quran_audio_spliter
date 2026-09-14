using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IAudioPeaksService
{
    Task<AudioPeaksDto> GetPeaksAsync(Guid sourceAudioId, double? startSeconds = null, double? durationSeconds = null, int maxPoints = 2000, CancellationToken ct = default);
    Task<AudioPeaksDto> GetPeaksBySurahAsync(int surahNumber, double? startSeconds = null, double? durationSeconds = null, int maxPoints = 2000, CancellationToken ct = default);
}
