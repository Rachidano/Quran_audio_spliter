using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IMushafImageService
{
    Task<MushafEditionDto> GetOrCreateDefaultEditionAsync(CancellationToken cancellationToken = default);
    Task<MushafPageDto?> GetPageAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default);
    Task<MushafPageViewerDto> GetPageViewerContextAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AyahImageRegionDto>> GetAyahRegionsAsync(long ayahId, CancellationToken cancellationToken = default);
    Task<AyahImageRegionDto> CreateAyahRegionAsync(CreateAyahRegionRequest request, CancellationToken cancellationToken = default);
    Task<AyahImageRegionDto> UpdateAyahRegionAsync(long regionId, UpdateAyahRegionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAyahRegionAsync(long regionId, CancellationToken cancellationToken = default);

    Task<AudioSegmentImageRegionDto> CreateSegmentImageRegionAsync(CreateSegmentImageRegionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioSegmentImageRegionDto>> GetSegmentImageRegionsAsync(Guid segmentId, CancellationToken cancellationToken = default);
    Task DeleteSegmentImageRegionAsync(long regionId, CancellationToken cancellationToken = default);

    Task<MushafPageDto> ImportPageAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default);
    Task<int> EnsurePagesImportedAsync(int fromPage, int toPage, int? editionId = null, CancellationToken cancellationToken = default);
}
