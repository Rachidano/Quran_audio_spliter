using System.Text.RegularExpressions;
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

    [GeneratedRegex(@"^(\d{1,3})\s*[:\s\-]\s*(\d{1,3})$")]
    private static partial Regex SurahAyahReferenceRegex();

    public QuranReaderService(QuranDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0) return Array.Empty<SearchResultDto>();
        var match = SurahAyahReferenceRegex().Match(query.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var surah) || !int.TryParse(match.Groups[2].Value, out var ayah))
            return Array.Empty<SearchResultDto>();
        var found = await GetAyahAsync(surah, ayah, cancellationToken);
        if (found == null) return Array.Empty<SearchResultDto>();
        var info = await GetSurahByNumberAsync(surah, cancellationToken);
        return new[] { new SearchResultDto(surah, found.SurahNameArabic, info?.NameLatin ?? "", ayah, "", "") };
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
