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

    public async Task<IReadOnlyList<AyahDto>> GetAyahsBySurahAsync(int surahNumber, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.QuranCorpora.AnyAsync(c => c.Code == CorpusResolutionService.MarwCorpusCode, cancellationToken))
            return await GetCanonicalAyahsAsync(surahNumber, cancellationToken);
        var surah = await _dbContext.Surahs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Number == surahNumber, cancellationToken);

        if (surah == null) return Array.Empty<AyahDto>();

        var ayahs = await _dbContext.Ayahs
            .AsNoTracking()
            .Where(a => a.SurahId == surah.Id)
            .OrderBy(a => a.AyahNumber)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Reciter)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Riwaya)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Wajh)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.AudioAsset)
            .ToListAsync(cancellationToken);

        return ayahs.Select(a => MapToAyahDto(surah.Number, surah.NameArabic, a)).ToList();
    }

    public async Task<AyahDto?> GetAyahAsync(int surahNumber, int ayahNumber, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.QuranCorpora.AnyAsync(c => c.Code == CorpusResolutionService.MarwCorpusCode, cancellationToken))
            return (await GetCanonicalAyahsAsync(surahNumber, cancellationToken)).FirstOrDefault(a => a.AyahNumber == ayahNumber);
        var surah = await _dbContext.Surahs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Number == surahNumber, cancellationToken);

        if (surah == null) return null;

        var ayah = await _dbContext.Ayahs
            .AsNoTracking()
            .Where(a => a.SurahId == surah.Id && a.AyahNumber == ayahNumber)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Reciter)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Riwaya)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.Wajh)
            .Include(a => a.Recitations.Where(r => r.IsPublished && r.Status == RecitationStatus.Published))
                .ThenInclude(r => r.AudioAsset)
            .FirstOrDefaultAsync(cancellationToken);

        if (ayah == null) return null;

        return MapToAyahDto(surah.Number, surah.NameArabic, ayah);
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResultDto>();

        query = query.Trim();

        // 1. Recherche par référence sourate:ayah (ex: "2:255" ou "1:1")
        var refMatch = SurahAyahReferenceRegex().Match(query);
        if (refMatch.Success &&
            int.TryParse(refMatch.Groups[1].Value, out var sNum) &&
            int.TryParse(refMatch.Groups[2].Value, out var aNum))
        {
            var singleAyah = await GetAyahAsync(sNum, aNum, cancellationToken);
            if (singleAyah != null)
            {
                var surah = await GetSurahByNumberAsync(sNum, cancellationToken);
                return new List<SearchResultDto>
                {
                    new(singleAyah.SurahNumber, singleAyah.SurahNameArabic, surah?.NameLatin ?? "", singleAyah.AyahNumber, singleAyah.TextWarsh, singleAyah.TextWarsh)
                };
            }
        }

        // 2. Recherche textuelle arabe normalisée
        var normalizedQuery = ArabicTextNormalizer.Normalize(query);
        if (await _dbContext.QuranCorpora.AnyAsync(c => c.Code == CorpusResolutionService.MarwCorpusCode, cancellationToken))
        {
            var matches = await _dbContext.CorpusAyahs.AsNoTracking()
                .Where(a => a.Corpus!.Code == "qpc-warsh-v10-candidate" && a.SearchText.Contains(normalizedQuery))
                .OrderBy(a => a.NumberInQuran).Take(Math.Clamp(maxResults, 1, 100)).ToListAsync(cancellationToken);
            var names = await _dbContext.Surahs.AsNoTracking().ToDictionaryAsync(s => s.Number, cancellationToken);
            return matches.Select(a => new SearchResultDto(a.SurahNumber, names[a.SurahNumber].NameArabic,
                names[a.SurahNumber].NameLatin, a.AyahNumber, a.TextArabic, a.TextArabic)).ToList();
        }

        var queryable = _dbContext.Ayahs
            .AsNoTracking()
            .Include(a => a.Surah)
            .Where(a => a.SearchText.Contains(normalizedQuery))
            .OrderBy(a => a.NumberInQuran)
            .Take(maxResults);

        var matchedAyahs = await queryable.ToListAsync(cancellationToken);

        return matchedAyahs.Select(a => new SearchResultDto(
            a.Surah.Number,
            a.Surah.NameArabic,
            a.Surah.NameLatin,
            a.AyahNumber,
            a.TextWarsh,
            a.TextWarsh
        )).ToList();
    }

    private async Task<IReadOnlyList<AyahDto>> GetCanonicalAyahsAsync(int surahNumber, CancellationToken ct)
    {
        var references = await _dbContext.CorpusAyahs.AsNoTracking()
            .Where(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == surahNumber)
            .OrderBy(a => a.AyahNumber).ToListAsync(ct);
        var texts = await _dbContext.CorpusAyahs.AsNoTracking()
            .Where(a => a.Corpus!.Code == "qpc-warsh-v10-candidate" && a.SurahNumber == surahNumber)
            .ToDictionaryAsync(a => a.AyahNumber, ct);
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
            texts.TryGetValue(a.AyahNumber, out var text) ? text.TextArabic : "النص الرقمي قيد التحقق؛ راجع صورة المصحف.",
            a.PageNumberHint, null, null,
            segments.Where(s => s.Reference == $"{surahNumber}:{a.AyahNumber}").OrderBy(s => s.StartMs).Select(s => new RecitationDto(
                s.PublishedAudioAssetId!.Value, s.SourceAudio.Reciter.NameArabic, s.SourceAudio.Reciter.NameLatin,
                s.SourceAudio.Riwaya.NameArabic, s.Wajh?.Code, s.Wajh?.NameArabic,
                "/audio/" + s.PublishedAudioAsset!.StorageKey, s.PublishedAudioAsset.DurationMs,
                s.PublishedAudioAsset.SizeBytes, s.Status.ToString(), true)).ToList())).ToList();
    }

    private static AyahDto MapToAyahDto(int surahNumber, string surahNameArabic, Domain.Entities.Ayah ayah)
    {
        var recitations = ayah.Recitations.Select(r => new RecitationDto(
            r.Id,
            r.Reciter.NameArabic,
            r.Reciter.NameLatin,
            r.Riwaya.NameArabic,
            r.Wajh?.Code,
            r.Wajh?.NameArabic,
            $"/audio/{r.AudioAsset.StorageKey}",
            r.AudioAsset.DurationMs,
            r.AudioAsset.SizeBytes,
            r.Status.ToString(),
            r.IsPublished
        )).ToList();

        return new AyahDto(
            ayah.Id,
            surahNumber,
            surahNameArabic,
            ayah.AyahNumber,
            ayah.NumberInQuran,
            ayah.TextWarsh,
            ayah.PageNumber,
            ayah.LineStart,
            ayah.LineEnd,
            recitations
        );
    }
}
