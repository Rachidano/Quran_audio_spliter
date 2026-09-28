using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Quran.Application.Common;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public partial class QuranReaderService : IQuranReaderService
{
    private readonly QuranDbContext _dbContext;

    private readonly IMemoryCache? _searchCache;

    public QuranReaderService(QuranDbContext dbContext, IMemoryCache? searchCache = null)
    {
        _dbContext = dbContext;
        _searchCache = searchCache;
    }

    public async Task<IReadOnlyList<SurahDto>> GetAllSurahsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Surahs
            .AsNoTracking()
            .OrderBy(s => s.Number)
            .Select(s => new SurahDto(
                s.Number,
                s.NameArabic,
                s.NameLatin,
                s.NameTranslation,
                s.AyahCount,
                s.RevelationOrder,
                s.RevelationType.ToString(),
                s.IsActive
            ))
            .ToListAsync(cancellationToken);
        var counts = await _dbContext.CorpusAyahs.Where(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode)
            .GroupBy(a => a.SurahNumber).Select(g => new { Number = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Number, cancellationToken);
        return result.Select(s => counts.TryGetValue(s.Number, out var count) ? s with { AyahCount = count.Count } : s).ToList();
    }

    public async Task<SurahDto?> GetSurahByNumberAsync(int number, CancellationToken cancellationToken = default)
    {
        var surah = await _dbContext.Surahs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Number == number, cancellationToken);

        if (surah == null) return null;
        var canonicalCount = await _dbContext.CorpusAyahs.CountAsync(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == number, cancellationToken);

        return new SurahDto(
            surah.Number,
            surah.NameArabic,
            surah.NameLatin,
            surah.NameTranslation,
            canonicalCount > 0 ? canonicalCount : surah.AyahCount,
            surah.RevelationOrder,
            surah.RevelationType.ToString(),
            surah.IsActive
        );
    }

    public Task<IReadOnlyList<AyahDto>> GetAyahsBySurahAsync(int surahNumber, CancellationToken cancellationToken = default) =>
        GetCanonicalAyahsAsync(surahNumber, cancellationToken);

    public async Task<AyahDto?> GetAyahAsync(int surahNumber, int ayahNumber, CancellationToken cancellationToken = default) =>
        (await GetCanonicalAyahsAsync(surahNumber, cancellationToken)).FirstOrDefault(a => a.AyahNumber == ayahNumber);

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, int maxResults = int.MaxValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200 || maxResults <= 0) return Array.Empty<SearchResultDto>();
        if (QuranSearchQuery.TryParseReference(query, out var surah, out var ayah))
        {
            return await (from a in _dbContext.CorpusAyahs.AsNoTracking()
                          join s in _dbContext.Surahs on a.SurahNumber equals s.Number
                          where a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == surah && a.AyahNumber == ayah
                          select new SearchResultDto(s.Number, s.NameArabic, s.NameLatin, a.AyahNumber, "", "", a.PageNumberHint, false))
                          .Take(1).ToListAsync(cancellationToken);
        }
        var term = SearchKey(query);
        if (term.Count(char.IsLetter) < 3) return Array.Empty<SearchResultDto>();
        var expandedTerm = SearchKey(query.Replace("\u0670", "ا"));
        var index = _searchCache == null ? await BuildSearchIndex(cancellationToken)
            : await _searchCache.GetOrCreateAsync("warsh-candidate-search-v2", async entry => {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await BuildSearchIndex(cancellationToken);
            });
        return index!.Where(row => MatchesPhrase(row, term.Split(' '), expandedTerm.Split(' ')))
            .Take(maxResults).Select(row => row.Result).ToList();
    }

    private sealed record SearchEntry(string[] Words, string[] ExpandedWords, SearchResultDto Result);

    private static bool MatchesPhrase(SearchEntry row, string[] words, string[] expanded)
    {
        // Dagger-alif may be silent in one query word (ذلك) and explicit in another (الكتاب).
        // Compare each word's variants, keeping phrase order and adjacency.
        for (var offset = 0; offset <= row.Words.Length - words.Length; offset++)
        {
            var matches = true;
            for (var i = 0; i < words.Length; i++)
            {
                var plain = row.Words[offset + i];
                var full = row.ExpandedWords.ElementAtOrDefault(offset + i) ?? plain;
                var alternate = expanded.ElementAtOrDefault(i) ?? words[i];
                bool WordMatches(string source, string term) => words.Length == 1
                    ? source.Contains(term, StringComparison.Ordinal)
                    : string.Equals(source, term, StringComparison.Ordinal);
                if (!WordMatches(plain, words[i]) && !WordMatches(full, words[i])
                    && !WordMatches(plain, alternate) && !WordMatches(full, alternate))
                { matches = false; break; }
            }
            if (matches) return true;
        }
        return false;
    }

    private static string SearchKey(string input) => QuranSearchQuery.Normalize(input);

    private async Task<List<SearchEntry>> BuildSearchIndex(CancellationToken ct)
    {
        // Candidate text is a provisional search preview; the reading view remains the MARW image.
        var rows = await (from candidate in _dbContext.CorpusAyahs.AsNoTracking()
                          join canonical in _dbContext.CorpusAyahs.AsNoTracking()
                            on new { candidate.SurahNumber, candidate.AyahNumber } equals new { canonical.SurahNumber, canonical.AyahNumber }
                          join surah in _dbContext.Surahs on canonical.SurahNumber equals surah.Number
                          where candidate.Corpus!.Code == "qpc-warsh-v10-candidate"
                            && canonical.Corpus!.Code == CorpusResolutionService.MarwCorpusCode
                          orderby canonical.SurahNumber, canonical.AyahNumber
                          select new { candidate.TextArabic, canonical.SurahNumber, canonical.AyahNumber,
                              canonical.PageNumberHint, surah.NameArabic, surah.NameLatin }).ToListAsync(ct);
        return rows.Select(row => new SearchEntry(SearchKey(row.TextArabic).Split(' '), SearchKey(row.TextArabic.Replace("\u0670", "ا")).Split(' '),
            new SearchResultDto(row.SurahNumber, row.NameArabic, row.NameLatin, row.AyahNumber, row.TextArabic, "", row.PageNumberHint, true))).ToList();
    }

    private async Task<IReadOnlyList<AyahDto>> GetCanonicalAyahsAsync(int surahNumber, CancellationToken ct)
    {
        var references = await _dbContext.CorpusAyahs.AsNoTracking()
            .Where(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == surahNumber)
            .OrderBy(a => a.AyahNumber).ToListAsync(ct);
        var name = await _dbContext.Surahs.Where(s => s.Number == surahNumber).Select(s => s.NameArabic).FirstOrDefaultAsync(ct) ?? "";
        var prefix = surahNumber + ":";
        var segments = await _dbContext.AudioSegments.AsNoTracking()
            .Include(s => s.SourceAudio).ThenInclude(s => s.Reciter)
            .Include(s => s.SourceAudio).ThenInclude(s => s.Riwaya)
            .Include(s => s.Wajh).Include(s => s.PublishedAudioAsset)
            .Where(s => s.Edition == MarwPilotCorpus.EditionCode && s.Reference != null && s.Reference.StartsWith(prefix)
                && s.Status == AudioSegmentStatus.Published && s.PublishedAudioAssetId != null
                && (s.Wajh == null || s.Wajh.ReviewStatus == "Approved"))
            .ToListAsync(ct);
        return references.Select(a => new AyahDto(a.Id, surahNumber, name, a.AyahNumber, a.NumberInQuran,
            string.Empty,
            a.PageNumberHint, null, null,
            segments.Where(s => s.Reference == $"{surahNumber}:{a.AyahNumber}").OrderBy(s => s.StartMs).Select(s => new RecitationDto(
                s.PublishedAudioAssetId!.Value, s.SourceAudio.Reciter.NameArabic, s.SourceAudio.Reciter.NameLatin,
                s.SourceAudio.Riwaya.NameArabic, s.Wajh?.Code, s.Wajh?.NameArabic,
                "/audio/" + s.PublishedAudioAsset!.StorageKey, s.PublishedAudioAsset.DurationMs,
                s.PublishedAudioAsset.SizeBytes, s.Status.ToString(), true)).ToList())).ToList();
    }

}
