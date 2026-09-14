using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Domain.Enums;

namespace Quran.Application.Interfaces;

public interface IAdminValidationService
{
    Task<AdminDashboardDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminRecitationItemDto>> GetRecitationsForValidationAsync(int? surahNumber, RecitationStatus? statusFilter, CancellationToken cancellationToken = default);
    Task ApproveRecitationAsync(long recitationId, string? comment, string? validatorUserId, CancellationToken cancellationToken = default);
    Task PublishRecitationAsync(long recitationId, string? comment, string? validatorUserId, CancellationToken cancellationToken = default);
    Task RejectRecitationAsync(long recitationId, string comment, string? validatorUserId, CancellationToken cancellationToken = default);
    Task UpdateWajhAsync(long recitationId, int wajhId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Wajh>> GetAvailableAwjuhAsync(CancellationToken cancellationToken = default);
}
