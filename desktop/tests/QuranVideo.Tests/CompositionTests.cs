using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class CompositionTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly CompositionService _compositionService;
    private readonly string _testAudioPath;

    public CompositionTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_comp_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _imageService = new AyahImageService(_workspaceService);
        _compositionService = new CompositionService(_workspaceService, _imageService);

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

        db.Ayahs.Add(new AyahEntity
        {
            Id = "1:1",
            SurahNumber = 1,
            AyahNumber = 1,
            VerseNo = 1,
            PageNumber = 1
        });

        db.Regions.Add(new AyahRegionEntity
        {
            AyahId = "1:1",
            PageNumber = 1,
            OrderIndex = 1,
            PixelX = 150,
            PixelY = 200,
            PixelWidth = 700,
            PixelHeight = 80
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task PrepareComposition_WithoutAnnotatedPassage_ReturnsFailure()
    {
        var (ok, error, comp) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("لم يتم تسجيل أو تحديد أي مقطع صوتي", error);
        Assert.Null(comp);
    }

    [Fact]
    public async Task PrepareComposition_WithValidPassage_CreatesReadyComposition()
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
                EndTimeSec = 5.0,
                Kind = PassageKind.Full,
                IsValidatedByUser = true,
                IsSelectedForExport = true,
                Revision = 1
            });
            await db.SaveChangesAsync();
        }

        await _imageService.ApproveImageAsync(1, 1);
        var (ok, error, comp) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(comp);
        Assert.Equal(CompositionStatus.Ready, comp.Status);
        Assert.Equal(1, comp.SurahNumber);
        Assert.Equal(1, comp.AyahNumber);
        Assert.Equal("Horizontal", comp.Orientation);
        Assert.Equal(0.5, comp.StartTimeSec);
        Assert.Equal(5.0, comp.EndTimeSec);
        Assert.True(File.Exists(comp.ImagePath));
    }

    [Fact]
    public async Task PrepareComposition_WhenPassageRevisionChanges_SetsNeedsReview()
    {
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        int passageId;
        using (var db = new AppDbContext(dbPath))
        {
            var p = new AyahPassageEntity
            {
                AudioSourceId = "husary_murattal_warsh:001",
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                StartTimeSec = 0.5,
                EndTimeSec = 5.0,
                IsSelectedForExport = true,
                Revision = 1
            };
            db.Passages.Add(p);
            await db.SaveChangesAsync();
            passageId = p.Id;
        }

        // Initial prepare
        var (ok1, _, comp1) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);
        Assert.True(ok1);
        Assert.NotNull(comp1);

        // Update passage revision (retake or fine-tuned edit)
        using (var db = new AppDbContext(dbPath))
        {
            var p = await db.Passages.FindAsync(passageId);
            p!.Revision = 2;
            p.StartTimeSec = 0.6;
            await db.SaveChangesAsync();
        }

        // Re-prepare: must detect revision difference and flag NeedsReview
        var (ok2, _, comp2) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);
        Assert.True(ok2);
        Assert.NotNull(comp2);
        Assert.Equal(CompositionStatus.NeedsReview, comp2.Status);
    }

    [Fact]
    public async Task RenderComposition_ProducesValidMp4AndManifest()
    {
        if (!File.Exists(_testAudioPath))
        {
            // Skip rendering if no audio file present in test runner
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
                EndTimeSec = 2.0, // Short 2-second snippet for fast test
                IsSelectedForExport = true,
                Revision = 1
            });
            await db.SaveChangesAsync();
        }

        var (prepOk, _, comp) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);
        Assert.True(prepOk);
        Assert.NotNull(comp);

        var (renderOk, renderErr, renderedComp) = await _compositionService.RenderCompositionAsync(comp.Id);

        Assert.True(renderOk, renderErr);
        Assert.Equal(CompositionStatus.Completed, renderedComp.Status);
        Assert.Equal(1920, renderedComp.Width);
        Assert.Equal(1080, renderedComp.Height);
        Assert.NotEmpty(renderedComp.VideoSha256);

        var fullVideoPath = Path.Combine(_testWorkspaceDir, renderedComp.RelativeVideoPath);
        Assert.True(File.Exists(fullVideoPath));
        Assert.True(new FileInfo(fullVideoPath).Length > 1000);

        // Verify sidecar manifest JSON (L08-07)
        var fullManifestPath = Path.Combine(_testWorkspaceDir, renderedComp.ManifestPath);
        Assert.True(File.Exists(fullManifestPath));

        var manifestJson = await File.ReadAllTextAsync(fullManifestPath);
        var manifest = JsonSerializer.Deserialize<VideoProductionManifest>(manifestJson);

        Assert.NotNull(manifest);
        Assert.Equal(1, manifest.SurahNumber);
        Assert.Equal(1, manifest.AyahNumber);
        Assert.Equal("Horizontal", manifest.Orientation);
        Assert.Equal(1920, manifest.Dimensions.Width);
        Assert.Equal(1080, manifest.Dimensions.Height);
        Assert.Equal("libx264", manifest.Ffmpeg.VideoCodec);
        Assert.Equal("aac", manifest.Ffmpeg.AudioCodec);
        Assert.Equal(renderedComp.VideoSha256, manifest.VideoSha256);

        // Titre et description de publication écrits à côté de la vidéo
        var publicationText = await File.ReadAllTextAsync(PublicationTextService.GetTextPath(fullVideoPath));
        Assert.Contains("سورة الفاتحة - الآية 1", publicationText);
        Assert.Contains(PublicationTextService.Riwaya, publicationText);
        Assert.Contains(PublicationTextService.MushafSource, publicationText);
    }

    [Fact]
    public async Task RenderComposition_VerticalOrientation_Produces1080x1920()
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

        var (prepOk, _, comp) = await _compositionService.PrepareCompositionAsync(1, 1, VideoOrientation.Vertical);
        Assert.True(prepOk);
        Assert.NotNull(comp);
        Assert.Equal("Vertical", comp.Orientation);

        var (renderOk, renderErr, renderedComp) = await _compositionService.RenderCompositionAsync(comp.Id);

        Assert.True(renderOk, renderErr);
        Assert.Equal(CompositionStatus.Completed, renderedComp.Status);
        Assert.Equal(1080, renderedComp.Width);
        Assert.Equal(1920, renderedComp.Height);
        Assert.NotEmpty(renderedComp.VideoSha256);

        var fullVideoPath = Path.Combine(_testWorkspaceDir, renderedComp.RelativeVideoPath);
        Assert.True(File.Exists(fullVideoPath));
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
        catch
        {
            // Ignore temp cleanup errors
        }
    }
}
