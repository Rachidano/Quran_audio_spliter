using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Projets « فيديوهاتي » créés par l'export studio individuel : un projet par source audio,
/// sans écraser les projets d'un autre récitateur ni ceux du wizard.
/// </summary>
public class StudioExportProjectTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly string _dbPath;

    public StudioExportProjectTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_studio_export_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(_dbPath);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, recursive: true);
        }
        catch
        {
        }
    }

    private static VideoCompositionEntity Composition(string audioSourceId, string reciterId, int passageRevision = 1) => new()
    {
        AyahId = "1:2",
        SurahNumber = 1,
        AyahNumber = 2,
        ReciterId = reciterId,
        AudioSourceId = audioSourceId,
        PassageId = 10,
        PassageRevision = passageRevision,
        StartTimeSec = 4.0,
        EndTimeSec = 9.5,
        ImageRecipeId = 3,
        ImageRevision = 1,
        ImagePath = "media/images/ayahs/ayah_001_002_rev1.png",
        Orientation = "Horizontal",
        Status = CompositionStatus.Completed,
        RelativeVideoPath = $"media/videos/ayah_001_002_{reciterId}_h_p10_r{passageRevision}_i1.mp4"
    };

    [Fact]
    public async Task SaveStudioExportProject_SameAudioSourceTwice_UpdatesSingleProject()
    {
        using var vm = new StudioViewModel(_workspaceService, surahNumber: 1, ayahNumber: 2);

        await vm.SaveStudioExportProjectAsync(Composition("saber_abdelhakam_warsh:001", "saber_abdelhakam", 1), null, null);
        await vm.SaveStudioExportProjectAsync(Composition("saber_abdelhakam_warsh:001", "saber_abdelhakam", 2), null, null);

        using var db = new AppDbContext(_dbPath);
        var projects = await db.Projects.Include(p => p.SelectedAyahs).ToListAsync();
        var project = Assert.Single(projects);
        Assert.EndsWith("_r2_i1.mp4", project.OutputVideoPath);
        var ayah = Assert.Single(project.SelectedAyahs);
        Assert.Equal(2, ayah.PassageRevision);
        Assert.Equal(1, await db.ProjectAyahs.CountAsync());
    }

    [Fact]
    public async Task SaveStudioExportProject_OtherReciter_DoesNotOverwriteFirstReciterProject()
    {
        using var vm = new StudioViewModel(_workspaceService, surahNumber: 1, ayahNumber: 2);

        await vm.SaveStudioExportProjectAsync(Composition("saber_abdelhakam_warsh:001", "saber_abdelhakam"), null, null);
        await vm.SaveStudioExportProjectAsync(Composition("husary_murattal_warsh:001", "husary"), null, null);

        using var db = new AppDbContext(_dbPath);
        var projects = await db.Projects.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, projects.Count);
        Assert.Equal("saber_abdelhakam_warsh:001", projects[0].AudioSourceId);
        Assert.Contains("saber_abdelhakam", projects[0].OutputVideoPath);
        Assert.Equal("husary_murattal_warsh:001", projects[1].AudioSourceId);
        Assert.Contains("husary", projects[1].OutputVideoPath);
    }

    [Fact]
    public async Task SaveStudioExportProject_WizardProjectSameRange_IsNeverOverwritten()
    {
        const string audioSourceId = "saber_abdelhakam_warsh:001";
        int wizardProjectId;
        using (var db = new AppDbContext(_dbPath))
        {
            var wizardProject = new VideoProjectEntity
            {
                Title = "مشروع المعالج",
                SurahNumber = 1,
                StartAyahNumber = 2,
                EndAyahNumber = 2,
                ReciterId = "saber_abdelhakam",
                AudioSourceId = audioSourceId,
                Orientation = "Horizontal",
                WatermarkText = "قناتي",
                Status = "Completed",
                OutputVideoPath = "media/videos/project_p1_001_002_002_saber_abdelhakam_h_r1.mp4",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Projects.Add(wizardProject);
            await db.SaveChangesAsync();
            wizardProjectId = wizardProject.Id;
        }

        using var vm = new StudioViewModel(_workspaceService, surahNumber: 1, ayahNumber: 2);
        await vm.SaveStudioExportProjectAsync(Composition(audioSourceId, "saber_abdelhakam"), null, null);

        using var verifyDb = new AppDbContext(_dbPath);
        var projects = await verifyDb.Projects.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, projects.Count);
        var untouched = projects.Single(p => p.Id == wizardProjectId);
        Assert.Equal("مشروع المعالج", untouched.Title);
        Assert.Equal("قناتي", untouched.WatermarkText);
        Assert.StartsWith("media/videos/project_p1_", untouched.OutputVideoPath);
    }
}

/// <summary>
/// Marges de correction d'image : récupération des pixels coupés, alignement au bord de page
/// et aperçu de comparaison fidèle à l'extraction.
/// </summary>
public class AyahImageMarginTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly string _dbPath;

    public AyahImageMarginTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_margins_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        var mushafDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "pages");
        Directory.CreateDirectory(mushafDir);
        using (var page = new Bitmap(1000, 1500, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(page))
        {
            g.Clear(Color.White);
            // Signe placé juste au-dessus de la zone de l'ayah 1:1 (coupé par la région)
            g.FillRectangle(Brushes.Black, 400, 180, 20, 8);
            // Encre au bord gauche de la page, à l'intérieur de la zone de l'ayah 1:2
            g.FillRectangle(Brushes.Black, 10, 620, 4, 10);
            page.Save(Path.Combine(mushafDir, "page-001.png"), ImageFormat.Png);
        }

        _dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(_dbPath);
        db.Database.EnsureCreated();
        db.Surahs.Add(new SurahEntity { Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 7, StartPage = 1, EndPage = 1 });
        db.Ayahs.Add(new AyahEntity { Id = "1:1", SurahNumber = 1, AyahNumber = 1, VerseNo = 1, PageNumber = 1 });
        db.Ayahs.Add(new AyahEntity { Id = "1:2", SurahNumber = 1, AyahNumber = 2, VerseNo = 2, PageNumber = 1 });
        db.Regions.Add(new AyahRegionEntity { AyahId = "1:1", PageNumber = 1, OrderIndex = 1, PixelX = 150, PixelY = 200, PixelWidth = 700, PixelHeight = 80 });
        db.Regions.Add(new AyahRegionEntity { AyahId = "1:2", PageNumber = 1, OrderIndex = 1, PixelX = 5, PixelY = 600, PixelWidth = 300, PixelHeight = 60 });
        db.SaveChanges();

        _imageService = new AyahImageService(_workspaceService);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, recursive: true);
        }
        catch
        {
        }
    }

    private static bool HasDarkPixel(string imagePath)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(imagePath));
        using var bmp = new Bitmap(stream);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y).GetBrightness() < 0.2f) return true;
        return false;
    }

    private static Color PixelAt(string imagePath, int x, int y)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(imagePath));
        using var bmp = new Bitmap(stream);
        return bmp.GetPixel(x, y);
    }

    [Fact]
    public async Task ApplyCorrection_TopMargin_RecoversSignCutAboveRegion()
    {
        var (ok, initialPath, _) = await _imageService.GetOrExtractAyahImageAsync(1, 1);
        Assert.True(ok);
        Assert.False(HasDarkPixel(initialPath));

        var (corrected, correctedPath, recipe) = await _imageService.ApplyCorrectionAsync(1, 1, 20, 0, 0, 0);

        Assert.True(corrected);
        Assert.Equal(20, recipe.MarginTop);
        Assert.True(HasDarkPixel(correctedPath));
    }

    [Fact]
    public async Task ApplyCorrection_RectBeyondPageEdge_KeepsInkAtItsRelativePosition()
    {
        // La région démarre à x=5 : +20 px à gauche la fait déborder de la page (x=-15).
        var (ok, path, _) = await _imageService.ApplyCorrectionAsync(1, 2, 0, 0, 20, 0);
        Assert.True(ok);

        // Cadre : x = max(0, 5 - 6) - 20 = -20 ; y = 600 - 6 = 594.
        // L'encre de la page en (10..13, 620..629) doit tomber en (30..33, 26..35).
        Assert.True(PixelAt(path, 31, 30).GetBrightness() < 0.2f);
        Assert.True(PixelAt(path, 16, 30).GetBrightness() > 0.8f);
    }

    [Fact]
    public async Task GenerateSideBySideComparison_ReflectsMarginCorrections()
    {
        await _imageService.GetOrExtractAyahImageAsync(1, 1);
        var before = File.ReadAllBytes(await _imageService.GenerateSideBySideComparisonAsync(1, 1));

        await _imageService.ApplyCorrectionAsync(1, 1, 20, 0, 0, 0);
        var after = File.ReadAllBytes(await _imageService.GenerateSideBySideComparisonAsync(1, 1));

        Assert.False(before.AsSpan().SequenceEqual(after));
    }
}
