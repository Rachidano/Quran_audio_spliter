using System.IO;
using System.Text.Json;
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
    string NormalizedText,
    string ExpandedNormalizedText)
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

    public static async Task EnsureTextsImportedAsync(AppDbContext db, string? baseDir = null)
    {
        if (await db.Texts.AnyAsync()) return;

        var candidates = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pack", "source.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "source.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "source.json"),
            Path.GetFullPath("data/reference/warsh-qpc-v10/source.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\data\reference\warsh-qpc-v10\source.json")
        };

        if (!string.IsNullOrEmpty(baseDir))
        {
            candidates.Insert(0, Path.Combine(baseDir, "pack", "source.json"));
            candidates.Insert(1, Path.Combine(baseDir, "data", "source.json"));
            candidates.Insert(2, Path.Combine(baseDir, "source.json"));
            candidates.Insert(3, Path.Combine(baseDir, "..", "pack", "source.json"));
        }

        string? foundPath = null;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                foundPath = c;
                break;
            }
        }

        if (foundPath == null) return;

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(foundPath));
            var entities = new List<AyahTextEntity>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var s = item.GetProperty("sura_no").GetInt32();
                var a = item.GetProperty("aya_no").GetInt32();
                var text = item.GetProperty("aya_text").GetString() ?? "";
                entities.Add(new AyahTextEntity
                {
                    CorpusId = "warsh-qpc-v10",
                    AyahId = $"{s}:{a}",
                    SurahNumber = s,
                    AyahNumber = a,
                    OriginalText = text
                });
            }

            if (entities.Count > 0)
            {
                db.Texts.AddRange(entities);
                await db.SaveChangesAsync();
                LoggingService.Instance.LogInfo($"Textes du Coran importés avec succès depuis {foundPath} ({entities.Count} ayat).");
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Échec de l'importation automatique des textes : {ex.Message}");
        }
    }

    public async Task InitializeAsync(string dbPath)
    {
        if (_isInitialized || !File.Exists(dbPath)) return;

        using var db = new AppDbContext(dbPath);
        if (!await db.Ayahs.AnyAsync()) return;

        // Ensure texts table is populated
        await EnsureTextsImportedAsync(db, Path.GetDirectoryName(dbPath));

        var ayahs = await db.Ayahs
            .Include(a => a.Surah)
            .OrderBy(a => a.VerseNo)
            .ToListAsync();

        var texts = await db.Texts.ToDictionaryAsync(t => t.AyahId, t => t.OriginalText);

        _index.Clear();
        foreach (var a in ayahs)
        {
            var origText = texts.TryGetValue(a.Id, out var txt) ? txt : $"الآية {a.AyahNumber}";
            var normText = ArabicTextNormalizer.NormalizeForSearch(origText, expandDaggerAlif: false);
            var expText = ArabicTextNormalizer.NormalizeForSearch(origText, expandDaggerAlif: true);
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
                normText,
                expText));
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

        var normQuery = ArabicTextNormalizer.NormalizeForSearch(trimmed, expandDaggerAlif: false);
        var expQuery = ArabicTextNormalizer.NormalizeForSearch(trimmed, expandDaggerAlif: true);
        if (string.IsNullOrWhiteSpace(normQuery) && string.IsNullOrWhiteSpace(expQuery))
        {
            return new SearchPagedResult(Array.Empty<AyahSearchEntry>(), 1, pageSize, 0, null);
        }

        var queryWords = normQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expQueryWords = expQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool MatchesAyah(AyahSearchEntry a)
        {
            // Exact phrase or substring match across plain or expanded forms
            if ((!string.IsNullOrWhiteSpace(normQuery) && (a.NormalizedText.Contains(normQuery, StringComparison.Ordinal) || a.ExpandedNormalizedText.Contains(normQuery, StringComparison.Ordinal))) ||
                (!string.IsNullOrWhiteSpace(expQuery) && (a.NormalizedText.Contains(expQuery, StringComparison.Ordinal) || a.ExpandedNormalizedText.Contains(expQuery, StringComparison.Ordinal))))
            {
                return true;
            }

            // Ordered words match for multi-word phrases
            if (queryWords.Length > 1)
            {
                int lastIdx = 0;
                bool allFound = true;
                for (int i = 0; i < queryWords.Length; i++)
                {
                    var w = queryWords[i];
                    var ew = i < expQueryWords.Length ? expQueryWords[i] : w;
                    int idx = a.NormalizedText.IndexOf(w, lastIdx, StringComparison.Ordinal);
                    if (idx == -1) idx = a.ExpandedNormalizedText.IndexOf(w, lastIdx, StringComparison.Ordinal);
                    if (idx == -1) idx = a.NormalizedText.IndexOf(ew, lastIdx, StringComparison.Ordinal);
                    if (idx == -1) idx = a.ExpandedNormalizedText.IndexOf(ew, lastIdx, StringComparison.Ordinal);

                    if (idx == -1)
                    {
                        allFound = false;
                        break;
                    }
                    lastIdx = idx + w.Length;
                }
                if (allFound) return true;
            }

            return false;
        }

        var results = _index
            .Where(MatchesAyah)
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
