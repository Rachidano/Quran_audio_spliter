using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IQuranQualityAuditService
{
    Task<GlobalQualityReportDto> GenerateGlobalQualityReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualityAnomalyDto>> AuditSurahConsistencyAsync(int surahNumber, CancellationToken cancellationToken = default);
}
