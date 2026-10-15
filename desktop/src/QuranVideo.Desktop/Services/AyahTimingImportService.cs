using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed record AyahTiming(int AyahNumber, double StartTimeSec, double EndTimeSec);

public sealed record TimingImportResult(
    bool Success,
    string? ErrorMessage,
    int ImportedCount,
    int UpdatedCount,
    int SkippedManualCount,
    string? Warning = null);

/// <summary>
/// Importe les minutages ayah par ayah publiés par mp3quran.net pour une récitation
/// (ex. الحصري ورش, read=120) et les enregistre comme prises « proposées » (non validées).
/// Les prises faites à la main ne sont jamais modifiées. Le JSON est mis en cache dans
/// l'espace de travail : après le premier import, aucune connexion n'est nécessaire.
/// </summary>
public sealed class AyahTimingImportService
{
    public const string ApiUrlTemplate = "https://www.mp3quran.net/api/v3/ayat_timing?surah={0}&read={1}";

    /// <summary>Écart maximal toléré entre la fin de la dernière ayah et la fin du fichier audio.</summary>
    public const double MaxTrailingAudioSec = 60.0;

    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly WorkspaceService _workspace;
    private readonly HttpClient _http;

    public AyahTimingImportService(WorkspaceService workspace, HttpClient? httpClient = null)
    {
        _workspace = workspace;
        _http = httpClient ?? SharedHttp;
    }

    private AppDbContext Db() => new(_workspace.GetDatabasePath());

    public string GetCachePath(int readId, int surahNumber) =>
        Path.Combine(_workspace.WorkspacePath, "media", "audio", "timings", readId.ToString(CultureInfo.InvariantCulture), $"{surahNumber:D3}.json");

    public async Task<int?> GetTimingReadIdAsync(string collectionId)
    {
        using var db = Db();
        return await db.RecitationCollections
            .Where(c => c.Id == collectionId)
            .Select(c => c.TimingReadId)
            .FirstOrDefaultAsync();
    }

    /// <summary>Lit les minutages depuis le cache local, ou les télécharge puis les met en cache.</summary>
    public async Task<IReadOnlyList<AyahTiming>> GetTimingsAsync(int readId, int surahNumber, CancellationToken ct = default)
    {
        var cachePath = GetCachePath(readId, surahNumber);
        string json;
        if (File.Exists(cachePath))
        {
            json = await File.ReadAllTextAsync(cachePath, ct);
        }
        else
        {
            var url = string.Format(CultureInfo.InvariantCulture, ApiUrlTemplate, surahNumber, readId);
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(ct);
            var timings = Parse(json);

            // Écriture atomique : on ne met en cache qu'une réponse lisible
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".part";
            await File.WriteAllTextAsync(temp, json, ct);
            File.Move(temp, cachePath, overwrite: true);
            return timings;
        }

        return Parse(json);
    }

    public static IReadOnlyList<AyahTiming> Parse(string json)
    {
        var items = JsonSerializer.Deserialize<List<Mp3QuranTimingItem>>(json)
                    ?? throw new InvalidDataException("Réponse de minutage vide.");
        return items
            .Where(i => i.Ayah > 0)
            .Select(i => new AyahTiming(i.Ayah, i.StartTimeMs / 1000.0, i.EndTimeMs / 1000.0))
            .OrderBy(t => t.AyahNumber)
            .ToList();
    }

    /// <summary>
    /// Importe les minutages de la sourate pour la collection donnée.
    /// </summary>
    /// <param name="replaceProposed">Réécrit aussi les prises déjà proposées (non retouchées) par un import précédent.</param>
    /// <param name="offsetSec">Décalage global appliqué à toutes les bornes (version de fichier légèrement différente).</param>
    /// <param name="includeOpeningInFirstAyah">
    /// Fait commencer la première ayah au début du fichier (0 s) pour y inclure l'isti'adha et la basmala :
    /// les minutages publiés les excluent, alors que l'image de la première ayah (MARW) contient la basmala.
    /// La fin de la première ayah n'est pas modifiée.
    /// </param>
    public async Task<TimingImportResult> ImportAsync(
        string collectionId,
        int surahNumber,
        bool replaceProposed = false,
        double offsetSec = 0,
        bool includeOpeningInFirstAyah = true,
        CancellationToken ct = default)
    {
        var surah = SurahCatalogService.GetSurah(surahNumber);
        if (surah == null)
            return Fail("رقم السورة غير صحيح.");

        using var db = Db();
        var collection = await db.RecitationCollections.FirstOrDefaultAsync(c => c.Id == collectionId, ct);
        if (collection?.TimingReadId is not int readId)
            return Fail("لا تتوفر توقيتات جاهزة لهذا القارئ.");

        var sourceId = $"{collectionId}:{surahNumber:D3}";
        var audio = await db.AudioSources.FirstOrDefaultAsync(a => a.Id == sourceId, ct);
        if (audio == null)
            return Fail("نزّل تلاوة هذه السورة أولاً، ثم استورد التوقيتات الجاهزة.");

        IReadOnlyList<AyahTiming> raw;
        try
        {
            raw = await GetTimingsAsync(readId, surahNumber, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError($"Téléchargement des minutages {readId}/{surahNumber}", ex);
            return Fail($"تعذر تنزيل التوقيتات الجاهزة. تحقق من الاتصال بالإنترنت. {ex.Message}");
        }

        var timings = raw
            .Select(t => t with { StartTimeSec = Math.Max(0, t.StartTimeSec + offsetSec), EndTimeSec = t.EndTimeSec + offsetSec })
            .Select(t => includeOpeningInFirstAyah && t.AyahNumber == 1 ? t with { StartTimeSec = 0 } : t)
            .ToList();

        var validationError = Validate(timings, surah.AyahCount, audio.DurationSeconds);
        if (validationError != null)
        {
            LoggingService.Instance.LogWarning($"Minutages {readId}/{surahNumber} refusés : {validationError}");
            return Fail(validationError);
        }

        // Les prises sont liées aux ayat du corpus (clé étrangère)
        var knownAyahIds = (await db.Ayahs
                .Where(a => a.SurahNumber == surahNumber)
                .Select(a => a.Id)
                .ToListAsync(ct))
            .ToHashSet();
        if (knownAyahIds.Count < surah.AyahCount)
            return Fail("بيانات آيات هذه السورة غير مستوردة بعد في المكتبة.");

        string? warning = null;
        var expectedPrefix = UrlPrefix(collection.BaseUrlTemplate);
        if (!string.IsNullOrEmpty(expectedPrefix) &&
            !string.IsNullOrEmpty(audio.SourceUrl) &&
            !audio.SourceUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            warning = "تنبيه: الملف الصوتي لم يُنزَّل من المصدر الذي حُسبت عليه التوقيتات، تحقق من الحدود بالاستماع.";
        }
        else if (string.IsNullOrEmpty(audio.SourceUrl))
        {
            warning = "تنبيه: الملف الصوتي مستورد محلياً، تحقق من أنه نفس تسجيل المصدر بالاستماع إلى بعض الحدود.";
        }

        var existing = await db.Passages
            .Where(p => p.AudioSourceId == sourceId && p.SurahNumber == surahNumber)
            .ToListAsync(ct);

        int imported = 0, updated = 0, skipped = 0;
        var note = $"{AyahPassageEntity.ProposedTimingNotePrefix}{readId}";

        using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var t in timings)
        {
            var ayahId = $"{surahNumber}:{t.AyahNumber}";
            var forAyah = existing.Where(p => p.AyahNumber == t.AyahNumber).ToList();
            var proposed = forAyah.Where(p => p.IsProposedTiming).ToList();

            if (forAyah.Count > proposed.Count)
            {
                // Une prise faite (ou retouchée) à la main existe : ne jamais y toucher
                skipped++;
                continue;
            }

            if (proposed.Count > 0)
            {
                if (!replaceProposed)
                {
                    skipped++;
                    continue;
                }

                var p = proposed[0];
                if (Math.Abs(p.StartTimeSec - t.StartTimeSec) > 0.01 || Math.Abs(p.EndTimeSec - t.EndTimeSec) > 0.01)
                {
                    p.StartTimeSec = Math.Round(t.StartTimeSec, 3);
                    p.EndTimeSec = Math.Round(t.EndTimeSec, 3);
                    p.Revision++;
                    updated++;
                }
                p.Notes = note;
                continue;
            }

            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = sourceId,
                SurahNumber = surahNumber,
                AyahNumber = t.AyahNumber,
                AyahId = ayahId,
                StartTimeSec = Math.Round(t.StartTimeSec, 3),
                EndTimeSec = Math.Round(t.EndTimeSec, 3),
                Kind = PassageKind.Full,
                IsSelectedForExport = true,
                IsValidatedByUser = false,
                Notes = note,
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow
            });
            imported++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        LoggingService.Instance.LogInfo(
            $"Minutages {readId}/{surahNumber} importés pour {sourceId} : {imported} ajoutés, {updated} mis à jour, {skipped} conservés.");
        return new TimingImportResult(true, null, imported, updated, skipped, warning);
    }

    /// <summary>Retourne un message d'erreur (arabe) si les minutages ne correspondent pas à la sourate et au fichier.</summary>
    public static string? Validate(IReadOnlyList<AyahTiming> timings, int expectedAyahCount, double audioDurationSec)
    {
        if (timings.Count != expectedAyahCount ||
            !timings.Select(t => t.AyahNumber).SequenceEqual(Enumerable.Range(1, expectedAyahCount)))
        {
            return $"عدد الآيات في التوقيتات ({timings.Count}) لا يطابق عدد آيات السورة في رواية ورش ({expectedAyahCount}).";
        }

        double previousEnd = 0;
        foreach (var t in timings)
        {
            if (t.EndTimeSec <= t.StartTimeSec)
                return $"توقيت الآية {t.AyahNumber} غير صالح.";
            if (t.StartTimeSec < previousEnd - 0.05)
                return $"توقيتات الآية {t.AyahNumber} غير مرتبة زمنياً.";
            previousEnd = t.EndTimeSec;
        }

        if (audioDurationSec > 0)
        {
            var lastEnd = timings[^1].EndTimeSec;
            if (lastEnd > audioDurationSec + 0.5)
                return $"التوقيتات تتجاوز مدة الملف الصوتي ({lastEnd:F1} ث مقابل {audioDurationSec:F1} ث): الملف ليس نفس تسجيل المصدر.";
            if (audioDurationSec - lastEnd > MaxTrailingAudioSec)
                return $"مدة الملف الصوتي ({audioDurationSec:F1} ث) أطول بكثير من التوقيتات ({lastEnd:F1} ث): الملف ليس نفس تسجيل المصدر.";
        }

        return null;
    }

    private static string UrlPrefix(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        var brace = template.IndexOf('{');
        return brace > 0 ? template[..brace] : template;
    }

    private static TimingImportResult Fail(string message) => new(false, message, 0, 0, 0);

    private sealed class Mp3QuranTimingItem
    {
        [JsonPropertyName("ayah")] public int Ayah { get; set; }
        [JsonPropertyName("start_time")] public double StartTimeMs { get; set; }
        [JsonPropertyName("end_time")] public double EndTimeMs { get; set; }
    }
}
