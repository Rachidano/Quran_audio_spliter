using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NAudio.Wave;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class MultiAyahVideoTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly SequenceValidationService _sequenceValidator;
    private readonly CompositionService _compositionService;
    private readonly string _testAudioPath;
    private readonly string _testImage1;
    private readonly string _testImage2;
    private readonly string _testImage3;

    public MultiAyahVideoTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"multi_video_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _imageService = new AyahImageService(_workspaceService);
        _sequenceValidator = new SequenceValidationService(_workspaceService);
        _compositionService = new CompositionService(_workspaceService, _imageService, _sequenceValidator);

        // 1. Synthesize 10-second test audio file
        var audioDir = Path.Combine(_testWorkspaceDir, "media", "audio", "sources");
        Directory.CreateDirectory(audioDir);
        _testAudioPath = Path.Combine(audioDir, "test_surah_001.wav");
        CreateSynthesizedAudioFile(_testAudioPath, 10.0);

        // 2. Synthesize test ayah images
        var imagesDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "ayahs");
        Directory.CreateDirectory(imagesDir);

        _testImage1 = Path.Combine(imagesDir, "ayah_001_001.png");
        _testImage2 = Path.Combine(imagesDir, "ayah_001_002.png");
        _testImage3 = Path.Combine(imagesDir, "ayah_001_003.png");

        CreateDummyAyahImage(_testImage1, "1:1");
        CreateDummyAyahImage(_testImage2, "1:2");
        CreateDummyAyahImage(_testImage3, "1:3");
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_ContinuousSequence_ProducesCorrectDurationAndStreams()
    {
        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "multi_continuous.mp4");

        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.0, 1.5),
            new(2, _testImage2, 1.5, 3.2)
        };

        var req = new MultiAyahVideoRequest(
            _testAudioPath,
            timeline,
            outVideo,
            VideoOrientation.Horizontal);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal("h264", result.VideoCodec);
        Assert.Equal("aac", result.AudioCodec);
        // Total expected duration is 3.2s; allow standard AAC frame tolerance (0.15s)
        Assert.InRange(result.DurationSeconds, 3.10, 3.35);
        Assert.True(File.Exists(outVideo));
        Assert.True(new FileInfo(outVideo).Length > 1000);
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_VerticalOrientation_Produces1080x1920()
    {
        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "multi_vertical.mp4");

        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.5, 2.0),
            new(2, _testImage2, 2.0, 3.5)
        };

        var req = new MultiAyahVideoRequest(
            _testAudioPath,
            timeline,
            outVideo,
            VideoOrientation.Vertical);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1080, result.Width);
        Assert.Equal(1920, result.Height);
        Assert.InRange(result.DurationSeconds, 2.90, 3.15);
        Assert.True(File.Exists(outVideo));
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_WithDarkBackgroundAndWatermark_AppliesStylingSuccessfully()
    {
        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "multi_styled.mp4");

        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.0, 1.5),
            new(2, _testImage2, 1.5, 3.0)
        };

        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Dark,
            WatermarkType: WatermarkType.Text,
            WatermarkText: "قناة ورش عن نافع",
            WatermarkPosition: WatermarkPosition.BottomLeft,
            WatermarkOpacity: 0.85);

        var req = new MultiAyahVideoRequest(
            _testAudioPath,
            timeline,
            outVideo,
            VideoOrientation.Horizontal,
            settings);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.True(File.Exists(outVideo));
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_WithIntervalGapBetweenTakes_ConcatenatesCorrectly()
    {
        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "multi_with_gap.mp4");

        // Non-continuous takes: take 1 is [0.0..1.2] (duration 1.2s), take 2 is [3.0..4.8] (duration 1.8s)
        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.0, 1.2),
            new(2, _testImage2, 3.0, 4.8)
        };

        var req = new MultiAyahVideoRequest(
            _testAudioPath,
            timeline,
            outVideo,
            VideoOrientation.Horizontal);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.True(result.Success, result.ErrorMessage);
        // Total expected duration is 1.2 + 1.8 = 3.0s
        Assert.InRange(result.DurationSeconds, 2.90, 3.15);
        Assert.True(File.Exists(outVideo));
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_MissingAudio_ReturnsFailure()
    {
        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "missing_audio.mp4");
        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.0, 1.5)
        };

        var req = new MultiAyahVideoRequest(
            "C:\\non_existent_audio.mp3",
            timeline,
            outVideo,
            VideoOrientation.Horizontal);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.False(result.Success);
        Assert.Contains("ملف الصوت المصدر غير موجود", result.ErrorMessage);
    }

    [Fact]
    public async Task RenderProjectAsync_FullEndToEnd_ValidatesAndProducesCompletedProject()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        db.Database.EnsureCreated();

        // 1. Seed base entities
        db.Reciters.Add(new ReciterEntity { Id = "husary", NameArabic = "محمود خليل الحصري", NameLatin = "Al-Husary" });
        db.RecitationCollections.Add(new RecitationCollectionEntity
        {
            Id = "husary_murattal_warsh",
            ReciterId = "husary",
            TitleArabic = "المصحف المرتل برواية ورش"
        });

        var audioRelPath = Path.GetRelativePath(_testWorkspaceDir, _testAudioPath);
        db.AudioSources.Add(new AudioSourceEntity
        {
            Id = "husary_murattal_warsh:001",
            CollectionId = "husary_murattal_warsh",
            SurahNumber = 1,
            RelativeFilePath = audioRelPath,
            DurationSeconds = 10.0,
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

        db.Ayahs.AddRange(
            new AyahEntity { Id = "1:1", SurahNumber = 1, AyahNumber = 1, VerseNo = 1, PageNumber = 1 },
            new AyahEntity { Id = "1:2", SurahNumber = 1, AyahNumber = 2, VerseNo = 2, PageNumber = 1 }
        );

        // Passages
        var p1 = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            AyahId = "1:1",
            SurahNumber = 1,
            AyahNumber = 1,
            StartTimeSec = 0.0,
            EndTimeSec = 1.8,
            Kind = PassageKind.Full,
            IsValidatedByUser = true,
            Revision = 1
        };
        var p2 = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            AyahId = "1:2",
            SurahNumber = 1,
            AyahNumber = 2,
            StartTimeSec = 1.8,
            EndTimeSec = 3.6,
            Kind = PassageKind.Full,
            IsValidatedByUser = true,
            Revision = 1
        };
        db.Passages.AddRange(p1, p2);

        // Image Recipes
        var r1 = new AyahImageRecipeEntity
        {
            AyahId = "1:1",
            SurahNumber = 1,
            AyahNumber = 1,
            PageNumber = 1,
            RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage1),
            Status = AyahImageStatus.Approved,
            Revision = 1
        };
        var r2 = new AyahImageRecipeEntity
        {
            AyahId = "1:2",
            SurahNumber = 1,
            AyahNumber = 2,
            PageNumber = 1,
            RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage2),
            Status = AyahImageStatus.Approved,
            Revision = 1
        };
        db.ImageRecipes.AddRange(r1, r2);
        await db.SaveChangesAsync();

        // Video Project
        var project = new VideoProjectEntity
        {
            Title = "مشروع الفاتحة 1-2",
            SurahNumber = 1,
            StartAyahNumber = 1,
            EndAyahNumber = 2,
            ReciterId = "husary",
            AudioSourceId = "husary_murattal_warsh:001",
            Orientation = "Horizontal",
            Background = BackgroundType.Dark,
            Watermark = WatermarkType.Text,
            WatermarkText = "تلاوة تعليمية",
            WatermarkPosition = WatermarkPosition.TopLeft,
            WatermarkOpacity = 0.8,
            Status = "Draft",
            Revision = 1
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        project.SelectedAyahs.AddRange(
            new ProjectAyahEntity
            {
                ProjectId = project.Id,
                OrderIndex = 1,
                SurahNumber = 1,
                AyahNumber = 1,
                AyahId = "1:1",
                PassageId = p1.Id,
                PassageRevision = p1.Revision,
                StartTimeSec = p1.StartTimeSec,
                EndTimeSec = p1.EndTimeSec,
                ImageRecipeId = r1.Id,
                ImageRevision = r1.Revision,
                RelativeImagePath = r1.RelativeImagePath
            },
            new ProjectAyahEntity
            {
                ProjectId = project.Id,
                OrderIndex = 2,
                SurahNumber = 1,
                AyahNumber = 2,
                AyahId = "1:2",
                PassageId = p2.Id,
                PassageRevision = p2.Revision,
                StartTimeSec = p2.StartTimeSec,
                EndTimeSec = p2.EndTimeSec,
                ImageRecipeId = r2.Id,
                ImageRevision = r2.Revision,
                RelativeImagePath = r2.RelativeImagePath
            }
        );
        await db.SaveChangesAsync();

        // Render project
        var (ok, err, renderedProject) = await _compositionService.RenderProjectAsync(project.Id);

        Assert.True(ok, err);
        Assert.Equal("Completed", renderedProject.Status);
        Assert.Null(renderedProject.ErrorMessage);
        Assert.NotNull(renderedProject.OutputVideoPath);

        var fullOutPath = Path.Combine(_testWorkspaceDir, renderedProject.OutputVideoPath);
        Assert.True(File.Exists(fullOutPath));
        Assert.True(new FileInfo(fullOutPath).Length > 1000);
    }

    [Fact]
    public async Task GenerateMultiAyahVideo_AudioWithCoverArt_SucceedsWithoutVideoTagError()
    {
        // Create an MP3 file with embedded cover art (Stream 0:1 mjpeg)
        var mp3WithCover = Path.Combine(_testWorkspaceDir, "media", "audio", "with_cover.mp3");
        var coverPng = Path.Combine(_testWorkspaceDir, "cover.png");
        CreateDummyAyahImage(coverPng, "Cover");

        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(_testAudioPath);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(coverPng);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("1:v");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("libmp3lame");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("mjpeg");
        psi.ArgumentList.Add("-id3v2_version");
        psi.ArgumentList.Add("3");
        psi.ArgumentList.Add("-metadata:s:v");
        psi.ArgumentList.Add("title=\"Album cover\"");
        psi.ArgumentList.Add("-metadata:s:v");
        psi.ArgumentList.Add("comment=\"Cover (front)\"");
        psi.ArgumentList.Add(mp3WithCover);

        using (var proc = System.Diagnostics.Process.Start(psi)!)
        {
            await proc.WaitForExitAsync();
            Assert.Equal(0, proc.ExitCode);
        }

        var outVideo = Path.Combine(_testWorkspaceDir, "media", "videos", "multi_with_cover.mp4");
        var timeline = new List<MultiAyahTimelineEntry>
        {
            new(1, _testImage1, 0.0, 1.5),
            new(2, _testImage2, 1.5, 3.2)
        };

        var req = new MultiAyahVideoRequest(
            mp3WithCover,
            timeline,
            outVideo,
            VideoOrientation.Horizontal);

        var result = await FfmpegVideoGenerator.GenerateMultiAyahVideoAsync(req);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal("h264", result.VideoCodec);
        Assert.Equal("aac", result.AudioCodec);
        Assert.True(File.Exists(outVideo));
    }

    private static void CreateSynthesizedAudioFile(string filePath, double durationSeconds)
    {
        var sampleRate = 44100;
        var channels = 2;
        var waveFormat = new WaveFormat(sampleRate, 16, channels);
        using var writer = new WaveFileWriter(filePath, waveFormat);

        int totalSamples = (int)(sampleRate * durationSeconds * channels);
        short[] buffer = new short[totalSamples];
        for (int i = 0; i < totalSamples; i += 2)
        {
            short val = (short)(Math.Sin(2 * Math.PI * 440 * (i / 2) / sampleRate) * 12000);
            buffer[i] = val;
            buffer[i + 1] = val;
        }
        writer.WriteSamples(buffer, 0, buffer.Length);
    }

    private static void CreateDummyAyahImage(string filePath, string label)
    {
        using var bmp = new Bitmap(700, 140);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var brush = new SolidBrush(Color.Black);
        using var font = new Font("Arial", 20, FontStyle.Bold);
        g.DrawString($"Ayah {label}", font, brush, 50, 50);
        bmp.Save(filePath, ImageFormat.Png);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir))
            {
                Directory.Delete(_testWorkspaceDir, recursive: true);
            }
        }
        catch { }
    }
}
