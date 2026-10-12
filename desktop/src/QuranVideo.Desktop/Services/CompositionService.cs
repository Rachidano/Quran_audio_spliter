using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed class CompositionService
{
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;

    public CompositionService(WorkspaceService workspaceService, AyahImageService? imageService = null)
    {
        _workspaceService = workspaceService;
        _imageService = imageService ?? new AyahImageService(workspaceService);
    }

    private string GetDbPath() => Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");

    private AppDbContext CreateDbContext() => new(GetDbPath());

    public async Task<(bool Success, string? ErrorMessage, VideoCompositionEntity? Composition)> PrepareCompositionAsync(
        int surahNumber,
        int ayahNumber,
        VideoOrientation orientation,
        string reciterId = "husary", string? collectionId = null)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        var audioSourceId = $"{collectionId ?? (reciterId == "saber_abdelhakam" ? RecitationCatalogService.SaberCollection : reciterId + "_murattal_warsh")}:{surahNumber:D3}";
        var orientationStr = orientation == VideoOrientation.Horizontal ? "Horizontal" : "Vertical";

        using var db = CreateDbContext();

        // 1. Check passage (L08-01 & L08-02)
        var passage = await db.Passages
            .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId && p.IsSelectedForExport)
            .OrderByDescending(p => p.Revision)
            .FirstOrDefaultAsync();

        if (passage == null)
        {
            // Fallback: look for any passage for this ayah
            passage = await db.Passages
                .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId)
                .OrderByDescending(p => p.Revision)
                .FirstOrDefaultAsync();
        }

        if (passage == null)
        {
            return (false, "لم يتم تسجيل أو تحديد أي مقطع صوتي لهذه الآية في الاستوديو.", null);
        }

        // 2. Check image recipe (L08-01 & L08-02)
        var (imageOk, imagePath, recipe) = await _imageService.GetOrExtractAyahImageAsync(surahNumber, ayahNumber);
        if (!imageOk || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            return (false, "لم يتم استخراج صورة الآية بنجاح من صفحات المصحف.", null);
        }

        // 3. Evaluation de l'éligibilité et du statut (L08-01 & L08-02)
        bool isCompleteAyah = passage.Kind == PassageKind.Full;
        bool isPassageValidated = passage.IsValidatedByUser;
        bool isImageApproved = recipe.Status == AyahImageStatus.Approved;
        var computedStatus = (isCompleteAyah && isPassageValidated && isImageApproved)
            ? CompositionStatus.Ready
            : CompositionStatus.NeedsReview;

        // 4. Find or create composition entity
        var comp = await db.Compositions
            .FirstOrDefaultAsync(c => c.SurahNumber == surahNumber && c.AyahNumber == ayahNumber && c.AudioSourceId == audioSourceId && c.Orientation == orientationStr);

        if (comp == null)
        {
            comp = new VideoCompositionEntity
            {
                AyahId = ayahId,
                SurahNumber = surahNumber,
                AyahNumber = ayahNumber,
                ReciterId = reciterId,
                AudioSourceId = audioSourceId,
                PassageId = passage.Id,
                PassageRevision = passage.Revision,
                StartTimeSec = passage.StartTimeSec,
                EndTimeSec = passage.EndTimeSec,
                ImageRecipeId = recipe.Id,
                ImageRevision = recipe.Revision,
                ImagePath = imagePath,
                Orientation = orientationStr,
                Status = computedStatus,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Compositions.Add(comp);
        }
        else
        {
            // L08-01: Detect if source passage or image revision changed
            if (comp.PassageRevision != passage.Revision || comp.ImageRevision != recipe.Revision)
            {
                comp.Status = CompositionStatus.NeedsReview;
            }
            else if (comp.Status != CompositionStatus.Completed)
            {
                comp.Status = computedStatus;
            }

            comp.PassageId = passage.Id;
            comp.PassageRevision = passage.Revision;
            comp.StartTimeSec = passage.StartTimeSec;
            comp.EndTimeSec = passage.EndTimeSec;
            comp.ImageRecipeId = recipe.Id;
            comp.ImageRevision = recipe.Revision;
            comp.ImagePath = imagePath;
        }

        await db.SaveChangesAsync();
        return (true, null, comp);
    }

    public async Task<(bool Success, string? ErrorMessage, VideoCompositionEntity Composition)> RenderCompositionAsync(
        int compositionId,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var db = CreateDbContext();
        var comp = await db.Compositions.FindAsync(compositionId);
        if (comp == null)
        {
            return (false, "تكوين الفيديو غير موجود.", new VideoCompositionEntity());
        }

        comp.Status = CompositionStatus.Rendering;
        await db.SaveChangesAsync();
        progress?.Report(20);

        // Resolve audio source path strictly from database and workspace
        string? audioPath = null;
        var audioSource = await db.AudioSources.FirstOrDefaultAsync(s => s.Id == comp.AudioSourceId);
        if (audioSource != null && !string.IsNullOrWhiteSpace(audioSource.RelativeFilePath))
        {
            var p = _workspaceService.ToAbsolutePath(audioSource.RelativeFilePath);
            if (File.Exists(p)) audioPath = p;
        }

        if (audioPath == null)
        {
            comp.Status = CompositionStatus.Failed;
            comp.ErrorMessage = "ملف التلاوة الصوتية لهذه السورة غير متوفر محلياً.";
            await db.SaveChangesAsync();
            return (false, comp.ErrorMessage, comp);
        }

        // Prepare output video path (L08-07: versioned naming)
        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        Directory.CreateDirectory(videosDir);

        var orientSuffix = comp.Orientation == "Horizontal" ? "h" : "v";
        var fileName = $"ayah_{comp.SurahNumber:D3}_{comp.AyahNumber:D3}_{comp.ReciterId}_{orientSuffix}_p{comp.PassageId}_r{comp.PassageRevision}_i{comp.ImageRevision}.mp4";
        var finalVideoPath = Path.Combine(videosDir, fileName);

        var orientationEnum = comp.Orientation == "Horizontal" ? VideoOrientation.Horizontal : VideoOrientation.Vertical;

        progress?.Report(40);

        var req = new VideoGenerationRequest(
            comp.ImagePath,
            audioPath,
            comp.StartTimeSec,
            comp.EndTimeSec,
            finalVideoPath,
            orientationEnum);

        var result = await FfmpegVideoGenerator.GenerateAyahVideoAsync(req, cancellationToken);
        progress?.Report(80);

        if (!result.Success)
        {
            comp.Status = CompositionStatus.Failed;
            comp.ErrorMessage = result.ErrorMessage;
            await db.SaveChangesAsync();
            return (false, result.ErrorMessage, comp);
        }

        // Calculate SHA-256 of final MP4
        var videoSha256 = ComputeSha256(finalVideoPath);

        // L08-07: Generate Production Manifest JSON
        var manifestDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos", "manifests");
        Directory.CreateDirectory(manifestDir);

        var manifestFileName = $"ayah_{comp.SurahNumber:D3}_{comp.AyahNumber:D3}_{comp.ReciterId}_{orientSuffix}_p{comp.PassageId}_r{comp.PassageRevision}_i{comp.ImageRevision}_manifest.json";
        var manifestPath = Path.Combine(manifestDir, manifestFileName);

        var manifest = new VideoProductionManifest
        {
            ManifestVersion = "1.0",
            CompositionId = comp.Id,
            AyahId = comp.AyahId,
            SurahNumber = comp.SurahNumber,
            AyahNumber = comp.AyahNumber,
            Orientation = comp.Orientation,
            Dimensions = new Dimensions(result.Width, result.Height),
            DurationSeconds = result.DurationSeconds,
            Audio = new AudioDetails(
                comp.AudioSourceId,
                ComputeSha256(audioPath),
                comp.StartTimeSec,
                comp.EndTimeSec,
                comp.PassageRevision),
            Image = new ImageDetails(
                comp.ImagePath,
                ComputeSha256(comp.ImagePath),
                comp.ImageRevision),
            Ffmpeg = new FfmpegProfile("8.0-essentials", "libx264", "aac", 30, "yuv420p"),
            VideoSha256 = videoSha256,
            RenderedAtUtc = DateTime.UtcNow
        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        // Update composition entity
        comp.Status = CompositionStatus.Completed;
        comp.RelativeVideoPath = Path.GetRelativePath(_workspaceService.WorkspacePath, finalVideoPath);
        comp.VideoSha256 = videoSha256;
        comp.DurationSeconds = result.DurationSeconds;
        comp.Width = result.Width;
        comp.Height = result.Height;
        comp.ManifestPath = Path.GetRelativePath(_workspaceService.WorkspacePath, manifestPath);
        comp.RenderedAtUtc = DateTime.UtcNow;
        comp.ErrorMessage = null;

        await db.SaveChangesAsync();
        progress?.Report(100);

        return (true, null, comp);
    }

    private static string ComputeSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

public sealed class VideoProductionManifest
{
    [JsonPropertyName("manifestVersion")]
    public string ManifestVersion { get; set; } = "1.0";

    [JsonPropertyName("compositionId")]
    public int CompositionId { get; set; }

    [JsonPropertyName("ayahId")]
    public string AyahId { get; set; } = string.Empty;

    [JsonPropertyName("surahNumber")]
    public int SurahNumber { get; set; }

    [JsonPropertyName("ayahNumber")]
    public int AyahNumber { get; set; }

    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "Horizontal";

    [JsonPropertyName("dimensions")]
    public Dimensions Dimensions { get; set; } = new(1920, 1080);

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("audio")]
    public AudioDetails Audio { get; set; } = new(string.Empty, string.Empty, 0, 0, 1);

    [JsonPropertyName("image")]
    public ImageDetails Image { get; set; } = new(string.Empty, string.Empty, 1);

    [JsonPropertyName("ffmpeg")]
    public FfmpegProfile Ffmpeg { get; set; } = new("8.0", "libx264", "aac", 30, "yuv420p");

    [JsonPropertyName("videoSha256")]
    public string VideoSha256 { get; set; } = string.Empty;

    [JsonPropertyName("renderedAtUtc")]
    public DateTime RenderedAtUtc { get; set; } = DateTime.UtcNow;
}

public record Dimensions([property: JsonPropertyName("width")] int Width, [property: JsonPropertyName("height")] int Height);
public record AudioDetails(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("startSec")] double StartSec,
    [property: JsonPropertyName("endSec")] double EndSec,
    [property: JsonPropertyName("revision")] int Revision);
public record ImageDetails(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("revision")] int Revision);
public record FfmpegProfile(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("videoCodec")] string VideoCodec,
    [property: JsonPropertyName("audioCodec")] string AudioCodec,
    [property: JsonPropertyName("fps")] int Fps,
    [property: JsonPropertyName("pixelFormat")] string PixelFormat);
