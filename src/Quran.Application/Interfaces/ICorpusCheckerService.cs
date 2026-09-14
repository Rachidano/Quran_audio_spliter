using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface ICorpusCheckerService
{
    Task<CorpusCheckReport> CheckCorpusAsync(CancellationToken cancellationToken = default);
}
