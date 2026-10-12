using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public record BatchEnqueueResult(int EnqueuedCount, int SkippedCount, List<string> SkippedDetails);

public sealed class BatchExportService : IDisposable
{
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private CancellationTokenSource? _queueCts;
    private bool _isProcessingQueue;

    public bool IsProcessingQueue => _isProcessingQueue;

    public event Action<BatchExportJobEntity>? JobProgressChanged;
    public event Action<BatchExportJobEntity>? JobStatusChanged;
    public event Action? QueueFinished;

    public BatchExportService(WorkspaceService workspaceService, AyahImageService? imageService = null)
    {
        _workspaceService = workspaceService;
        _imageService = imageService ?? new AyahImageService(workspaceService);
    }

    private string GetDbPath() => Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
    private AppDbContext CreateDbContext() => new(GetDbPath());

    /// <summary>
    /// L09-03: Reclassifie au démarrage les travaux "Running" en "Interrupted".
    /// </summary>
    public async Task<int> RecoverInterruptedJobsAsync()
    {
        using var db = CreateDbContext();
        var interruptedJobs = await db.BatchJobs
            .Where(j => j.Status == BatchJobStatus.Running)
            .ToListAsync();

        if (interruptedJobs.Count > 0)
        {
            foreach (var job in interruptedJobs)
            {
                job.Status = BatchJobStatus.Interrupted;
                job.ErrorMessage = "توقف التصدير بشكل غير متوقع (انقطاع سابق للنظام).";
            }
            await db.SaveChangesAsync();
            LoggingService.Instance.LogInfo($"Recouvrement : {interruptedJobs.Count} travaux marqués 'Interrupted'.");
        }

        // L09-06: Nettoyer les fichiers temporaires orphelins dans media/videos/
        CleanupOrphanTempFiles();

        return interruptedJobs.Count;
    }

    /// <summary>
    /// L09-01 & L09-02: Ajoute une plage d'ayat à la file en figeant le snapshot exact des dépendances.
    /// Ne crée pas de vidéo vide pour les ayat non annotées.
    /// </summary>
    public async Task<BatchEnqueueResult> EnqueueRangeAsync(
        int surahNumber,
        int startAyah,
        int endAyah,
        VideoOrientation orientation,
        string reciterId = "husary", string? collectionId = null)
    {
        using var db = CreateDbContext();
        var audioSourceId = $"{collectionId ?? (reciterId == "saber_abdelhakam" ? RecitationCatalogService.SaberCollection : reciterId + "_murattal_warsh")}:{surahNumber:D3}";
        var orientationStr = orientation == VideoOrientation.Horizontal ? "Horizontal" : "Vertical";

        // Resolve audio source path
        var audioPath = ResolveAudioPath(audioSourceId);

        var enqueuedCount = 0;
        var skippedCount = 0;
        var skippedDetails = new List<string>();

        for (int ayah = startAyah; ayah <= endAyah; ayah++)
        {
            var ayahId = $"{surahNumber}:{ayah}";

            // 0. Anti-doublon : vérifier si un travail pour cette sourate, ayah et orientation est déjà en attente ou en cours
            var isAlreadyQueued = await db.BatchJobs.AnyAsync(j =>
                j.SurahNumber == surahNumber &&
                j.AyahNumber == ayah &&
                j.Orientation == orientationStr &&
                (j.Status == BatchJobStatus.Pending || j.Status == BatchJobStatus.Running));

            if (isAlreadyQueued)
            {
                skippedCount++;
                skippedDetails.Add($"الآية {ayah}: موجودة بالفعل في قائمة الانتظار.");
                continue;
            }

            // 1. Passage audio
            var passage = await db.Passages
                .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId && p.IsSelectedForExport)
                .OrderByDescending(p => p.Revision)
                .FirstOrDefaultAsync();

            if (passage == null)
            {
                passage = await db.Passages
                    .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId)
                    .OrderByDescending(p => p.Revision)
                    .FirstOrDefaultAsync();
            }

            if (passage == null)
            {
                skippedCount++;
                skippedDetails.Add($"الآية {ayah}: لم يتم تسجيل مقطع صوتي.");
                continue;
            }

            // 2. Image recipe
            var (imageOk, imagePath, recipe) = await _imageService.GetOrExtractAyahImageAsync(surahNumber, ayah);
            if (!imageOk || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                skippedCount++;
                skippedDetails.Add($"الآية {ayah}: تعذر استخراج صورة الآية من المصحف.");
                continue;
            }

            // 3. Freeze snapshot (L09-02)
            var job = new BatchExportJobEntity
            {
                SurahNumber = surahNumber,
                AyahNumber = ayah,
                AyahId = ayahId,
                Orientation = orientationStr,
                Status = BatchJobStatus.Pending,
                AudioSourceId = audioSourceId,
                AudioPath = audioPath ?? string.Empty,
                PassageRevision = passage.Revision,
                StartTimeSec = passage.StartTimeSec,
                EndTimeSec = passage.EndTimeSec,
                ImageRecipeId = recipe.Id,
                ImageRevision = recipe.Revision,
                ImagePath = imagePath,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.BatchJobs.Add(job);
            enqueuedCount++;
        }

        if (enqueuedCount > 0)
        {
            await db.SaveChangesAsync();
        }

        return new BatchEnqueueResult(enqueuedCount, skippedCount, skippedDetails);
    }

    /// <summary>
    /// L09-04: Exécute la file avec 1 seul encodeur simultané (Worker séquentiel).
    /// </summary>
    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_isProcessingQueue) return;

        await _workerLock.WaitAsync(cancellationToken);
        _isProcessingQueue = true;
        _queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            while (!_queueCts.Token.IsCancellationRequested)
            {
                BatchExportJobEntity? nextJob;
                using (var db = CreateDbContext())
                {
                    nextJob = await db.BatchJobs
                        .Where(j => j.Status == BatchJobStatus.Pending)
                        .OrderBy(j => j.CreatedAtUtc)
                        .FirstOrDefaultAsync();
                }

                if (nextJob == null)
                {
                    // Plus aucun travail en attente
                    break;
                }

                await ExecuteSingleJobAsync(nextJob.Id, _queueCts.Token);
            }
        }
        finally
        {
            _isProcessingQueue = false;
            _workerLock.Release();
            QueueFinished?.Invoke();
        }
    }

    private async Task ExecuteSingleJobAsync(int jobId, CancellationToken cancellationToken)
    {
        using var db = CreateDbContext();
        var job = await db.BatchJobs.FindAsync(jobId);
        if (job == null || job.Status != BatchJobStatus.Pending) return;

        // L09-05: Contrôle de l'espace disque (au moins 50 Mo requis)
        if (!CheckDiskSpace(_workspaceService.WorkspacePath, 50 * 1024 * 1024))
        {
            job.Status = BatchJobStatus.Failed;
            job.ErrorMessage = "مساحة القرص غير كافية لتصدير الفيديو (أقل من 50 ميغابايت).";
            await db.SaveChangesAsync();
            JobStatusChanged?.Invoke(job);
            return;
        }

        // Vérifier les dépendances figées
        if (!File.Exists(job.AudioPath))
        {
            var fallback = ResolveAudioPath(job.AudioSourceId);
            if (fallback != null && File.Exists(fallback))
            {
                job.AudioPath = fallback;
            }
            else
            {
                job.Status = BatchJobStatus.Failed;
                job.ErrorMessage = "ملف الصوت المصدر غير متوفر.";
                await db.SaveChangesAsync();
                JobStatusChanged?.Invoke(job);
                return;
            }
        }

        if (!File.Exists(job.ImagePath))
        {
            job.Status = BatchJobStatus.Failed;
            job.ErrorMessage = "صورة الآية غير متوفرة.";
            await db.SaveChangesAsync();
            JobStatusChanged?.Invoke(job);
            return;
        }

        job.Status = BatchJobStatus.Running;
        job.StartedAtUtc = DateTime.UtcNow;
        job.ProgressPercent = 10;
        await db.SaveChangesAsync();
        JobStatusChanged?.Invoke(job);

        var orientEnum = job.Orientation == "Horizontal" ? VideoOrientation.Horizontal : VideoOrientation.Vertical;
        var orientSuffix = orientEnum == VideoOrientation.Horizontal ? "h" : "v";

        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        Directory.CreateDirectory(videosDir);

        var fileName = $"ayah_{job.SurahNumber:D3}_{job.AyahNumber:D3}_{orientSuffix}_rev{job.PassageRevision}.mp4";
        var finalVideoPath = Path.Combine(videosDir, fileName);

        var req = new VideoGenerationRequest(
            job.ImagePath,
            job.AudioPath,
            job.StartTimeSec,
            job.EndTimeSec,
            finalVideoPath,
            orientEnum);

        var progress = new Progress<int>(p =>
        {
            job.ProgressPercent = p;
            JobProgressChanged?.Invoke(job);
        });

        try
        {
            var result = await FfmpegVideoGenerator.GenerateAyahVideoAsync(req, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                job.Status = BatchJobStatus.Cancelled;
                job.ErrorMessage = "تم إلغاء التصدير بطلب من المستخدم.";
            }
            else if (result.Success)
            {
                var sha = ComputeSha256(finalVideoPath);
                job.Status = BatchJobStatus.Completed;
                job.RelativeVideoPath = Path.GetRelativePath(_workspaceService.WorkspacePath, finalVideoPath);
                job.VideoSha256 = sha;
                job.DurationSeconds = result.DurationSeconds;
                job.ProgressPercent = 100;
                job.CompletedAtUtc = DateTime.UtcNow;
                job.ErrorMessage = null;

                // L09-05: Écrire le manifeste sidecar JSON
                await WriteJobManifestAsync(job, result.Width, result.Height, sha, cancellationToken);
            }
            else
            {
                job.Status = BatchJobStatus.Failed;
                job.ErrorMessage = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = BatchJobStatus.Cancelled;
            job.ErrorMessage = "تم إلغاء التصدير.";
        }
        catch (Exception ex)
        {
            job.Status = BatchJobStatus.Failed;
            job.ErrorMessage = $"خطأ أثناء المعالجة: {ex.Message}";
        }

        await db.SaveChangesAsync();
        JobStatusChanged?.Invoke(job);
    }

    private async Task WriteJobManifestAsync(BatchExportJobEntity job, int width, int height, string videoSha, CancellationToken ct)
    {
        var manifestDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos", "manifests");
        Directory.CreateDirectory(manifestDir);

        var orientSuffix = job.Orientation == "Horizontal" ? "h" : "v";
        var manifestFileName = $"ayah_{job.SurahNumber:D3}_{job.AyahNumber:D3}_{orientSuffix}_manifest.json";
        var manifestPath = Path.Combine(manifestDir, manifestFileName);

        var manifest = new VideoProductionManifest
        {
            ManifestVersion = "1.0",
            CompositionId = job.Id,
            AyahId = job.AyahId,
            SurahNumber = job.SurahNumber,
            AyahNumber = job.AyahNumber,
            Orientation = job.Orientation,
            Dimensions = new Dimensions(width, height),
            DurationSeconds = job.DurationSeconds,
            Audio = new AudioDetails(
                job.AudioSourceId,
                ComputeSha256(job.AudioPath),
                job.StartTimeSec,
                job.EndTimeSec,
                job.PassageRevision),
            Image = new ImageDetails(
                job.ImagePath,
                ComputeSha256(job.ImagePath),
                job.ImageRevision),
            Ffmpeg = new FfmpegProfile("8.0-essentials", "libx264", "aac", 30, "yuv420p"),
            VideoSha256 = videoSha,
            RenderedAtUtc = DateTime.UtcNow
        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);
    }

    public async Task<bool> CancelJobAsync(int jobId)
    {
        using var db = CreateDbContext();
        var job = await db.BatchJobs.FindAsync(jobId);
        if (job == null) return false;

        if (job.Status == BatchJobStatus.Pending)
        {
            job.Status = BatchJobStatus.Cancelled;
            await db.SaveChangesAsync();
            JobStatusChanged?.Invoke(job);
            return true;
        }

        if (job.Status == BatchJobStatus.Running)
        {
            _queueCts?.Cancel();
            return true;
        }

        return false;
    }

    public void CancelQueue()
    {
        _queueCts?.Cancel();
    }

    public async Task<bool> RetryJobAsync(int jobId)
    {
        using var db = CreateDbContext();
        var job = await db.BatchJobs.FindAsync(jobId);
        if (job == null) return false;

        if (job.Status == BatchJobStatus.Failed || job.Status == BatchJobStatus.Cancelled || job.Status == BatchJobStatus.Interrupted)
        {
            job.Status = BatchJobStatus.Pending;
            job.ErrorMessage = null;
            job.ProgressPercent = 0;
            job.RetryCount++;
            await db.SaveChangesAsync();
            JobStatusChanged?.Invoke(job);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteJobAsync(int jobId)
    {
        using var db = CreateDbContext();
        var job = await db.BatchJobs.FindAsync(jobId);
        if (job == null) return false;

        if (job.Status == BatchJobStatus.Running)
        {
            _queueCts?.Cancel();
        }

        db.BatchJobs.Remove(job);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> ClearFinishedJobsAsync()
    {
        using var db = CreateDbContext();
        var toRemove = await db.BatchJobs
            .Where(j => j.Status == BatchJobStatus.Completed ||
                        j.Status == BatchJobStatus.Cancelled ||
                        j.Status == BatchJobStatus.Failed ||
                        j.Status == BatchJobStatus.Interrupted)
            .ToListAsync();

        if (toRemove.Count > 0)
        {
            db.BatchJobs.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }
        return toRemove.Count;
    }

    public async Task<int> ClearAllPendingJobsAsync()
    {
        using var db = CreateDbContext();
        var toRemove = await db.BatchJobs
            .Where(j => j.Status != BatchJobStatus.Running)
            .ToListAsync();

        if (toRemove.Count > 0)
        {
            db.BatchJobs.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }
        return toRemove.Count;
    }

    public async Task<List<BatchExportJobEntity>> GetJobsAsync()
    {
        using var db = CreateDbContext();
        return await db.BatchJobs
            .OrderBy(j => j.Status == BatchJobStatus.Running ? 0 :
                          j.Status == BatchJobStatus.Pending ? 1 : 2)
            .ThenByDescending(j => j.CreatedAtUtc)
            .ToListAsync();
    }

    private string? ResolveAudioPath(string audioSourceId)
    {
        using var db = CreateDbContext();
        var source = db.AudioSources.SingleOrDefault(s => s.Id == audioSourceId);
        if (source == null || string.IsNullOrEmpty(source.RelativeFilePath)) return null;
        var path = _workspaceService.ToAbsolutePath(source.RelativeFilePath);
        return File.Exists(path) ? path : null;
    }

    private static bool CheckDiskSpace(string folderPath, long requiredBytes)
    {
        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            var driveInfo = new DriveInfo(Path.GetPathRoot(fullPath) ?? "C:\\");
            return driveInfo.AvailableFreeSpace >= requiredBytes;
        }
        catch
        {
            return true; // En cas d'erreur de lecture du lecteur, ne pas bloquer
        }
    }

    private void CleanupOrphanTempFiles()
    {
        try
        {
            var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
            if (!Directory.Exists(videosDir)) return;

            var tmpFiles = Directory.GetFiles(videosDir, "*.tmp.mp4");
            foreach (var f in tmpFiles)
            {
                try { File.Delete(f); } catch { }
            }
        }
        catch
        {
            // Ignorer erreurs de nettoyage
        }
    }

    private static string ComputeSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public void Dispose()
    {
        _queueCts?.Dispose();
        _workerLock.Dispose();
    }
}
