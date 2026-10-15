using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed class RecitationLibraryService
{
    private readonly WorkspaceService _workspace;
    private readonly HttpClient _http;
    private static readonly HttpClient SharedHttp = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly SemaphoreSlim Writes = new(1, 1);
    public RecitationLibraryService(WorkspaceService workspaceService, HttpClient? httpClient = null)
    { _workspace = workspaceService; _http = httpClient ?? SharedHttp; }
    private AppDbContext Db() => new(_workspace.GetDatabasePath());
    public Task InitializeDefaultCollectionAsync() => new RecitationCatalogService(_workspace).InitializeAsync();

    public async Task<AudioSourceEntity?> GetAudioSourceAsync(string collectionId, int surahNumber)
    {
        using var db = Db();
        return await db.AudioSources.AsNoTracking().FirstOrDefaultAsync(a => a.CollectionId == collectionId && a.SurahNumber == surahNumber);
    }

    public async Task<(bool Success, AudioSourceEntity? Source, string? ErrorMessage)> ImportLocalAudioAsync(
        string collectionId, int surahNumber, string localSourceFilePath)
        => await RegisterAsync(collectionId, surahNumber, localSourceFilePath, "", CancellationToken.None);

    private async Task<(bool Success, AudioSourceEntity? Source, string? ErrorMessage)> RegisterAsync(
        string collectionId, int surahNumber, string sourcePath, string sourceUrl, CancellationToken ct)
    {
        if (surahNumber is < 1 or > 114 || !File.Exists(sourcePath)) return (false, null, "الملف الصوتي أو رقم السورة غير صالح.");
        await Writes.WaitAsync(ct);
        try
        {
            using var db = Db();
            var collection = await db.RecitationCollections.SingleOrDefaultAsync(c => c.Id == collectionId, ct);
            if (collection == null) return (false, null, "القارئ غير مسجل.");
            var metadata = await ProbeAudioAsync(sourcePath, ct);
            if (metadata == null) return (false, null, "الملف المستلم ليس تسجيلاً صوتياً صالحاً. تحقق من رابط التحميل المباشر.");
            await using var stream = File.OpenRead(sourcePath);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            await stream.DisposeAsync();
            var sourceId = $"{collectionId}:{surahNumber:D3}";
            var existing = await db.AudioSources.SingleOrDefaultAsync(s => s.Id == sourceId, ct);
            if (existing != null && !string.Equals(existing.Sha256, hash, StringComparison.OrdinalIgnoreCase)
                && (await db.Passages.AnyAsync(p => p.AudioSourceId == sourceId, ct)
                    || await db.BatchJobs.AnyAsync(j => j.AudioSourceId == sourceId, ct)))
                return (false, null, "هذا التسجيل مرتبط بتوقيتات أو فيديوهات. أضف تسجيلاً جديداً باسم آخر للحفاظ على العمل السابق.");

            // Content-based path: invalid downloads and replacements never overwrite a previous file.
            var folder = Path.Combine(_workspace.WorkspacePath, "media", "audio", collectionId);
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, $"{surahNumber:D3}_{hash}.mp3");
            if (!File.Exists(dest))
            {
                var temp = dest + "." + Guid.NewGuid().ToString("N") + ".part";
                try
                {
                    await using (var input = File.OpenRead(sourcePath))
                    await using (var output = File.Create(temp)) await input.CopyToAsync(output, ct);
                    File.Move(temp, dest);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            existing ??= new AudioSourceEntity { Id = sourceId, CollectionId = collectionId, SurahNumber = surahNumber };
            if (db.Entry(existing).State == EntityState.Detached) db.AudioSources.Add(existing);
            existing.RelativeFilePath = Path.GetRelativePath(_workspace.WorkspacePath, dest);
            existing.SourceUrl = sourceUrl;
            existing.Sha256 = hash;
            existing.DurationSeconds = metadata.Duration;
            existing.SampleRate = metadata.SampleRate;
            existing.Channels = metadata.Channels;
            existing.FileSizeBytes = new FileInfo(dest).Length;
            existing.ImportedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return (true, existing, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Import audio", ex);
            return (false, null, $"تعذر حفظ التسجيل: {ex.Message}");
        }
        finally { Writes.Release(); }
    }

    public async Task<(bool Success, AudioSourceEntity? Source, string? ErrorMessage)> DownloadSurahAudioAsync(
        string collectionId, int surahNumber, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        string? temp = null;
        try
        {
            var links = await new RecitationCatalogService(_workspace).GetLinksAsync(collectionId);
            var url = links.GetValueOrDefault(surahNumber, "");
            if (!RecitationCatalogService.IsValidUrl(url)) return (false, null, "أدخل رابط هذه السورة في إعدادات روابط التلاوات أولاً.");
            var folder = Path.Combine(_workspace.WorkspacePath, "media", "audio", "downloads");
            Directory.CreateDirectory(folder);
            temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".part");
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
                return (false, null, "الرابط يشير إلى صفحة ويب وليس إلى ملف صوتي مباشر.");
            var total = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(temp))
            {
                var buffer = new byte[81920]; long received = 0; int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    progress?.Report(total > 0 ? received * 100.0 / total.Value : -1);
                }
                if (total.HasValue && total.Value != received) return (false, null, "انقطع التنزيل قبل اكتمال الملف. أعد المحاولة.");
            }
            return await RegisterAsync(collectionId, surahNumber, temp, url, cancellationToken);
        }
        catch (OperationCanceledException) { return (false, null, "تم إلغاء التنزيل."); }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Téléchargement audio", ex);
            return (false, null, $"تعذر تنزيل التلاوة. تحقق من الرابط والاتصال بالإنترنت. {ex.Message}");
        }
        finally { if (temp != null && File.Exists(temp)) File.Delete(temp); }
    }

    private sealed record AudioMetadata(double Duration, int SampleRate, int Channels);
    private static async Task<AudioMetadata?> ProbeAudioAsync(string path, CancellationToken ct)
    {
        var probe = await ExternalProcessRunner.RunAsync(
            FfmpegVideoGenerator.GetFfprobePath(),
            new[] { "-v", "error", "-show_format", "-show_streams", "-of", "json", path },
            ExternalProcessRunner.FfprobeInactivityTimeout,
            ct);
        if (!probe.Success) return null;
        using var doc = JsonDocument.Parse(probe.StandardOutput);
        foreach (var audio in doc.RootElement.GetProperty("streams").EnumerateArray())
        {
            if (audio.GetProperty("codec_type").GetString() != "audio") continue;
            if (!double.TryParse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || !double.IsFinite(duration) || duration <= 0) return null;
            var rate = int.Parse(audio.GetProperty("sample_rate").GetString()!, CultureInfo.InvariantCulture);
            var channels = audio.GetProperty("channels").GetInt32();
            return rate > 0 && channels > 0 ? new(duration, rate, channels) : null;
        }
        return null;
    }
}
