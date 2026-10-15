using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class BatchExportTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly BatchExportService _batchService;
    private readonly string _testAudioPath;

    public BatchExportTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_batch_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _imageService = new AyahImageService(_workspaceService);
        _batchService = new BatchExportService(_workspaceService, _imageService);

        // Copy or synthesize page image
        var mushafDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "pages");
        Directory.CreateDirectory(mushafDir);
        var pagePath = Path.Combine(mushafDir, "page-001.png");
        var realPage = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full/page-001.png");
        if (File.Exists(realPage))
        {
            File.Copy(realPage, pagePath, overwrite: true);
        }
        else
        {
            using var bmp = new Bitmap(1000, 1500);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            bmp.Save(pagePath, ImageFormat.Png);
        }

        // Copy audio test file
        var audioDir = Path.Combine(_testWorkspaceDir, "media", "audio", "sources");
        Directory.CreateDirectory(audioDir);
        _testAudioPath = Path.Combine(audioDir, "husary_murattal_warsh_001.mp3");

        var realAudio = Path.GetFullPath("desktop/doc/validation/prototype_output/fatiha_ayah1.mp3");
        if (!File.Exists(realAudio))
        {
            realAudio = Path.GetFullPath("data/audio/recitations/warsh/husary/001.mp3");
        }
        if (File.Exists(realAudio))
        {
            File.Copy(realAudio, _testAudioPath, overwrite: true);
        }

        // Initialize SQLite schema & seed
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        db.Database.EnsureCreated();

        db.Reciters.Add(new ReciterEntity { Id = "husary", NameArabic = "محمود خليل الحصري", NameLatin = "Al-Husary" });
        db.RecitationCollections.Add(new RecitationCollectionEntity
        {
            Id = "husary_murattal_warsh",
            ReciterId = "husary",
            TitleArabic = "المصحف المرتل برواية ورش"
        });

        db.AudioSources.Add(new AudioSourceEntity
        {
            Id = "husary_murattal_warsh:001",
            CollectionId = "husary_murattal_warsh",
            SurahNumber = 1,
            RelativeFilePath = "media/audio/sources/husary_murattal_warsh_001.mp3",
            DurationSeconds = 40.0,
            SampleRate = 44100,
            Channels = 2,
            FileSizeBytes = 100000
        });

        db.Surahs.Add(new SurahEntity
        {
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            StartPage = 1,
            EndPage = 1
        });

        for (int i = 1; i <= 7; i++)
        {
            db.Ayahs.Add(new AyahEntity
            {
                Id = $"1:{i}",
                SurahNumber = 1,
                AyahNumber = i,
                VerseNo = i,
                PageNumber = 1
            });
            db.Regions.Add(new AyahRegionEntity
            {
                AyahId = $"1:{i}",
                PageNumber = 1,
                OrderIndex = 1,
                PixelX = 100,
                PixelY = 100 + (i * 100),
                PixelWidth = 600,
                PixelHeight = 70
            });
        }

        db.SaveChanges();
    }

    [Fact]
    public async Task RecoverInterruptedJobs_WhenJobsWereRunning_MarksThemAsInterrupted()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.BatchJobs.Add(new BatchExportJobEntity
            {
                SurahNumber = 1,
                AyahNumber = 1,
                AyahId = "1:1",
                Status = BatchJobStatus.Running,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-4)
            });
            await db.SaveChangesAsync();
        }

        var recoveredCount = await _batchService.RecoverInterruptedJobsAsync();

        Assert.Equal(1, recoveredCount);

        using (var db = new AppDbContext(dbPath))
        {
            var job = await db.BatchJobs.FirstAsync();
            Assert.Equal(BatchJobStatus.Interrupted, job.Status);
            Assert.Contains("انقطاع سابق", job.ErrorMessage);
        }
    }

    [Fact]
    public async Task EnqueueRange_SkipsUnannotatedAyahs_AndFreezesSnapshot()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        // Annotate only Ayah 1 and 2, leave Ayah 3 unannotated
        using (var db = new AppDbContext(dbPath))
        {
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                StartTimeSec = 0.5,
                EndTimeSec = 4.0,
                IsSelectedForExport = true,
                Revision = 1
            });
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                AyahId = "1:2",
                SurahNumber = 1,
                AyahNumber = 2,
                StartTimeSec = 4.0,
                EndTimeSec = 8.5,
                IsSelectedForExport = true,
                Revision = 2
            });
            await db.SaveChangesAsync();
        }

        // Enqueue range 1..3
        var result = await _batchService.EnqueueRangeAsync(1, 1, 3, VideoOrientation.Horizontal);

        Assert.Equal(2, result.EnqueuedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(result.SkippedDetails);
        Assert.Contains("الآية 3", result.SkippedDetails[0]);

        // Verify snapshot in DB
        using (var db = new AppDbContext(dbPath))
        {
            var jobs = await db.BatchJobs.OrderBy(j => j.AyahNumber).ToListAsync();
            Assert.Equal(2, jobs.Count);

            // Job 1
            Assert.Equal(1, jobs[0].AyahNumber);
            Assert.Equal(BatchJobStatus.Pending, jobs[0].Status);
            Assert.Equal(0.5, jobs[0].StartTimeSec);
            Assert.Equal(4.0, jobs[0].EndTimeSec);
            Assert.Equal(1, jobs[0].PassageRevision);
            Assert.True(File.Exists(jobs[0].ImagePath));

            // Job 2
            Assert.Equal(2, jobs[1].AyahNumber);
            Assert.Equal(BatchJobStatus.Pending, jobs[1].Status);
            Assert.Equal(4.0, jobs[1].StartTimeSec);
            Assert.Equal(8.5, jobs[1].EndTimeSec);
            Assert.Equal(2, jobs[1].PassageRevision);
        }
    }

    [Fact]
    public async Task CancelJob_And_RetryJob_TransitionsStatesCorrectly()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        int jobId;
        using (var db = new AppDbContext(dbPath))
        {
            var job = new BatchExportJobEntity
            {
                SurahNumber = 1,
                AyahNumber = 1,
                AyahId = "1:1",
                Status = BatchJobStatus.Pending
            };
            db.BatchJobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        // Cancel job
        var cancelOk = await _batchService.CancelJobAsync(jobId);
        Assert.True(cancelOk);

        using (var db = new AppDbContext(dbPath))
        {
            var job = await db.BatchJobs.FindAsync(jobId);
            Assert.Equal(BatchJobStatus.Cancelled, job!.Status);
        }

        // Retry job
        var retryOk = await _batchService.RetryJobAsync(jobId);
        Assert.True(retryOk);

        using (var db = new AppDbContext(dbPath))
        {
            var job = await db.BatchJobs.FindAsync(jobId);
            Assert.Equal(BatchJobStatus.Pending, job!.Status);
            Assert.Equal(1, job.RetryCount);
        }
    }

    [Fact]
    public async Task ProcessQueue_RendersBatchJobSuccessfully()
    {
        if (!File.Exists(_testAudioPath))
        {
            return;
        }

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                StartTimeSec = 0.0,
                EndTimeSec = 1.5,
                IsSelectedForExport = true,
                Revision = 1
            });
            await db.SaveChangesAsync();
        }

        var enqueueResult = await _batchService.EnqueueRangeAsync(1, 1, 1, VideoOrientation.Horizontal);
        Assert.Equal(1, enqueueResult.EnqueuedCount);

        // Process queue
        await _batchService.ProcessQueueAsync();

        // Check result
        using (var db = new AppDbContext(dbPath))
        {
            var job = await db.BatchJobs.FirstAsync();
            Assert.Equal(BatchJobStatus.Completed, job.Status);
            Assert.NotEmpty(job.RelativeVideoPath);
            Assert.NotEmpty(job.VideoSha256);
            Assert.True(File.Exists(Path.Combine(_testWorkspaceDir, job.RelativeVideoPath)));

            // Titre et description de publication écrits à côté de la vidéo
            var textPath = PublicationTextService.GetTextPath(Path.Combine(_testWorkspaceDir, job.RelativeVideoPath));
            Assert.Contains($"الآية {job.AyahNumber}", await File.ReadAllTextAsync(textPath));
        }
    }

    [Fact]
    public async Task EnqueueRangeAsync_PreventsDuplicates_WhenJobsAlreadyPending()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                SurahNumber = 1,
                AyahNumber = 2,
                AyahId = "1:2",
                StartTimeSec = 0.0,
                EndTimeSec = 5.0,
                Kind = PassageKind.Full,
                IsSelectedForExport = true
            });
            await db.SaveChangesAsync();
        }

        // 1st enqueue: should succeed
        var result1 = await _batchService.EnqueueRangeAsync(1, 2, 2, VideoOrientation.Horizontal, "husary");
        Assert.Equal(1, result1.EnqueuedCount);
        Assert.Equal(0, result1.SkippedCount);

        // 2nd enqueue: should detect duplicate and skip
        var result2 = await _batchService.EnqueueRangeAsync(1, 2, 2, VideoOrientation.Horizontal, "husary");
        Assert.Equal(0, result2.EnqueuedCount);
        Assert.Equal(1, result2.SkippedCount);
        Assert.Contains(result2.SkippedDetails, d => d.Contains("موجودة بالفعل"));
    }

    [Fact]
    public async Task DeleteJobAsync_And_ClearFinishedJobs_WorkCorrectly()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        int jobId1, jobId2;
        using (var db = new AppDbContext(dbPath))
        {
            var j1 = new BatchExportJobEntity
            {
                SurahNumber = 1,
                AyahNumber = 3,
                AyahId = "1:3",
                Orientation = "Horizontal",
                Status = BatchJobStatus.Pending,
                AudioSourceId = "husary_murattal_warsh:001",
                AudioPath = _testAudioPath,
                CreatedAtUtc = DateTime.UtcNow
            };
            var j2 = new BatchExportJobEntity
            {
                SurahNumber = 1,
                AyahNumber = 4,
                AyahId = "1:4",
                Orientation = "Horizontal",
                Status = BatchJobStatus.Cancelled,
                AudioSourceId = "husary_murattal_warsh:001",
                AudioPath = _testAudioPath,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.BatchJobs.AddRange(j1, j2);
            await db.SaveChangesAsync();
            jobId1 = j1.Id;
            jobId2 = j2.Id;
        }

        // Delete single job
        var deleted = await _batchService.DeleteJobAsync(jobId1);
        Assert.True(deleted);

        using (var db = new AppDbContext(dbPath))
        {
            Assert.Null(await db.BatchJobs.FindAsync(jobId1));
            Assert.NotNull(await db.BatchJobs.FindAsync(jobId2));
        }

        // Clear finished / cancelled jobs
        var clearedCount = await _batchService.ClearFinishedJobsAsync();
        Assert.Equal(1, clearedCount);

        using (var db = new AppDbContext(dbPath))
        {
            Assert.Null(await db.BatchJobs.FindAsync(jobId2));
        }
    }

    [Fact]
    public async Task EnqueueRange_AudioOnly_SetsOrientationToAudio()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                StartTimeSec = 0.5,
                EndTimeSec = 4.0,
                IsSelectedForExport = true,
                Revision = 1
            });
            await db.SaveChangesAsync();
        }

        var result = await _batchService.EnqueueRangeAsync(
            1, 1, 1,
            VideoOrientation.Horizontal,
            reciterId: "husary",
            collectionId: "husary_murattal_warsh",
            isAudioOnly: true);

        Assert.Equal(1, result.EnqueuedCount);
        Assert.Equal(0, result.SkippedCount);

        using var dbVerify = new AppDbContext(dbPath);
        var job = await dbVerify.BatchJobs.FirstOrDefaultAsync(j => j.SurahNumber == 1 && j.AyahNumber == 1);
        Assert.NotNull(job);
        Assert.Equal("Audio", job.Orientation);
    }

    public void Dispose()
    {
        _batchService.Dispose();
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir))
            {
                Directory.Delete(_testWorkspaceDir, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }
}
