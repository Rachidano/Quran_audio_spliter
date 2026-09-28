using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IQuranReaderService
{
    Task<IReadOnlyList<SurahDto>> GetAllSurahsAsync(CancellationToken cancellationToken = default);
    Task<SurahDto?> GetSurahByNumberAsync(int number, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AyahDto>> GetAyahsBySurahAsync(int surahNumber, CancellationToken cancellationToken = default);
    Task<AyahDto?> GetAyahAsync(int surahNumber, int ayahNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, int maxResults = int.MaxValue, CancellationToken cancellationToken = default);
}
