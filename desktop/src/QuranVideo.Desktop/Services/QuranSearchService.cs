using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;

namespace QuranVideo.Desktop.Services;

public sealed record AyahSearchEntry(
    string AyahId,
    int SurahNumber,
    string SurahNameArabic,
    string SurahNameLatin,
    int AyahNumber,
    int VerseNo,
    int PageNumber,
    string OriginalText,
    string NormalizedText)
{
    public string FormattedReference => $"سورة {SurahNameArabic} ({AyahNumber})";
}

public sealed record SearchPagedResult(
    IReadOnlyList<AyahSearchEntry> Items,
    int PageNumber,
    int PageSize,
    int TotalResults,
    string? ValidationMessage);

public sealed class QuranSearchService
{
    private readonly List<AyahSearchEntry> _index = new();
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public int IndexedAyahsCount => _index.Count;

    public async Task InitializeAsync(string dbPath)
    {
        if (_isInitialized || !File.Exists(dbPath)) return;

        using var db = new AppDbContext(dbPath);
        if (!await db.Ayahs.AnyAsync()) return;

        var ayahs = await db.Ayahs
            .Include(a => a.Surah)
            .OrderBy(a => a.VerseNo)
            .ToListAsync();

        var texts = await db.Texts.ToDictionaryAsync(t => t.AyahId, t => t.OriginalText);

        _index.Clear();
        foreach (var a in ayahs)
        {
            var origText = texts.TryGetValue(a.Id, out var txt) ? txt : $"الآية {a.AyahNumber}";
            var normText = ArabicTextNormalizer.NormalizeForSearch(origText);
            var surahNameAr = a.Surah?.NameArabic ?? $"سورة {a.SurahNumber}";
            var surahNameLat = a.Surah?.NameLatin ?? $"Surah {a.SurahNumber}";

            _index.Add(new AyahSearchEntry(
                a.Id,
                a.SurahNumber,
                surahNameAr,
                surahNameLat,
                a.AyahNumber,
                a.VerseNo,
                a.PageNumber,
                origText,
                normText));
        }

        _isInitialized = true;
        LoggingService.Instance.LogInfo($"Index de recherche initialisé : {_index.Count} ayat indexées.");
    }

    public SearchPagedResult Search(string query, int pageNumber = 1, int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchPagedResult(Array.Empty<AyahSearchEntry>(), 1, pageSize, 0, null);
        }

        var trimmed = query.Trim();

        // 1. Try Reference Search (Independent of 3-letter minimum)
        if (TryParseReference(trimmed, out var targetSurah, out var targetAyah))
        {
            var match = _index.Where(a => a.SurahNumber == targetSurah && (targetAyah <= 0 || a.AyahNumber == targetAyah)).ToList();
            var paged = match.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new SearchPagedResult(paged, pageNumber, pageSize, match.Count, null);
        }

        // 2. Text Search (Requires 3 letters minimum)
        if (!ArabicTextNormalizer.HasMinimumLetters(trimmed))
        {
            return new SearchPagedResult(
                Array.Empty<AyahSearchEntry>(),
                1,
                pageSize,
                0,
                "يرجى إدخال 3 أحرف على الأقل للبحث في نص الآيات، أو كتابة رقم الآية (مثال 2:255).");
        }

        var normQuery = ArabicTextNormalizer.NormalizeForSearch(trimmed);
        if (string.IsNullOrWhiteSpace(normQuery))
        {
            return new SearchPagedResult(Array.Empty<AyahSearchEntry>(), 1, pageSize, 0, null);
        }

        // Multiple words must match ordered and contiguously
        var results = _index
            .Where(a => a.NormalizedText.Contains(normQuery, StringComparison.Ordinal))
            .ToList();

        var pagedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchPagedResult(pagedResults, pageNumber, pageSize, results.Count, null);
    }

    public static bool TryParseReference(string query, out int surahNumber, out int ayahNumber)
    {
        surahNumber = 0;
        ayahNumber = 0;

        var clean = query.Trim();

        // Case 1: "2:255" or "2-255" or "2 255"
        var digitsOnly = string.Concat(clean.Select(c => char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c));
        var matchNums = Regex.Match(digitsOnly, @"^(\d{1,3})\s*[:\s\-]\s*(\d{1,3})$");
        if (matchNums.Success &&
            int.TryParse(matchNums.Groups[1].Value, out surahNumber) &&
            int.TryParse(matchNums.Groups[2].Value, out ayahNumber))
        {
            var surahInfo = SurahCatalogService.GetSurah(surahNumber);
            if (surahInfo != null)
            {
                return ayahNumber >= 1 && ayahNumber <= surahInfo.AyahCount;
            }
            return false;
        }

        // Case 2: Named surah + ayah number, e.g. "البقرة 255" or "سورة الفاتحة 7"
        var surahMatch = Regex.Match(clean, @"^(?:سورة\s+)?([\p{L}\s]+?)\s+(\d{1,3})$");
        if (surahMatch.Success && int.TryParse(surahMatch.Groups[2].Value, out ayahNumber))
        {
            var surahNameNorm = ArabicTextNormalizer.Normalize(surahMatch.Groups[1].Value);
            var catalog = SurahCatalogService.GetAllSurahs();
            var found = catalog.FirstOrDefault(s =>
                ArabicTextNormalizer.Normalize(s.NameArabic).Equals(surahNameNorm, StringComparison.OrdinalIgnoreCase) ||
                s.NameLatin.Equals(surahMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                surahNumber = found.Number;
                return ayahNumber >= 1 && ayahNumber <= found.AyahCount;
            }
        }

        return false;
    }
}
