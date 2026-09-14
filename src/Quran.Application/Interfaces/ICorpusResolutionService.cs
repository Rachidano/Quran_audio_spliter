using Quran.Application.DTOs;
using Quran.Domain.Entities;

namespace Quran.Application.Interfaces;

public interface ICorpusResolutionService
{
    Task<QuranCorpus?> GetCorpusByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<QuranCorpus>> GetAllCorporaAsync(CancellationToken ct = default);
    Task<CorpusAyah?> ResolveAyahAsync(string corpusCode, int surahNumber, int ayahNumber, CancellationToken ct = default);
    Task<ReferenceMappingResult> ResolveMappingAsync(string sourceCorpusCode, string targetCorpusCode, int surahNumber, int ayahNumber, CancellationToken ct = default);
    Task<IReadOnlyList<ReferenceMapping>> GetMappingsForSurahAsync(int surahNumber, string? sourceCorpus = null, string? targetCorpus = null, CancellationToken ct = default);
    Task<ReferenceMapping> SaveMappingAsync(ReferenceMapping mapping, CancellationToken ct = default);
    Task EnsureCorporaInitializedAsync(CancellationToken ct = default);
}
