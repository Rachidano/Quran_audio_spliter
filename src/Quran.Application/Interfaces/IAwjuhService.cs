using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IAwjuhService
{
    // Search & Retrieval
    Task<List<WajhDto>> SearchAsync(AwjuhSearchFilter filter, CancellationToken cancellationToken = default);
    Task<WajhDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WajhDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    // Queries for Reader & Editor
    Task<List<WajhDto>> GetAvailableAwjuhForReaderAsync(int? surahNumber, string? reference, CancellationToken cancellationToken = default);
    Task<List<WajhDto>> GetCandidateAwjuhForEditorAsync(int? surahNumber, string? reference, CancellationToken cancellationToken = default);

    // CRUD & Lifecycle
    Task<WajhDto> CreateDraftAsync(CreateWajhDto dto, string? userId, CancellationToken cancellationToken = default);
    Task<WajhDto> UpdateAsync(int id, UpdateWajhDto dto, string? userId, CancellationToken cancellationToken = default);
    Task<WajhDto> ReviewAsync(int id, ReviewWajhDto dto, string? userId, CancellationToken cancellationToken = default);
    Task<WajhDto> DuplicateAsync(int id, string newCode, string? userId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(int id, int version, CancellationToken cancellationToken = default);

    // Usage & Impact Report
    Task<WajhUsageReportDto> GetUsageReportAsync(int id, CancellationToken cancellationToken = default);

    // Transmission Routes, Sources & Constraints
    Task<List<TransmissionRouteDto>> GetRoutesAsync(CancellationToken cancellationToken = default);
    Task<List<ReferenceSourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<List<CombinationConstraintDto>> GetConstraintsAsync(string? routeCode = null, CancellationToken cancellationToken = default);

    // Documentary Coverage Matrix
    Task<List<DocumentaryCoverageEntryDto>> GetCoverageMatrixAsync(string? routeCode = null, CancellationToken cancellationToken = default);
    Task<DocumentaryCoverageEntryDto> UpdateCoverageEntryAsync(UpdateCoverageEntryDto dto, string? userId, CancellationToken cancellationToken = default);

    // Safe Initialization / Seeding
    Task<int> EnsureSeedDataAsync(bool force = false, CancellationToken cancellationToken = default);
}
