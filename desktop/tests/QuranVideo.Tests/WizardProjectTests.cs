using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class WizardProjectTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AnnotationService _annotationService;
    private readonly WaveformService _waveformService;
    private readonly AyahImageService _imageService;
    private readonly CompositionService _compositionService;
    private readonly string _testAudioPath;
    private readonly string _testImage1;
    private readonly string _testImage2;

    public WizardProjectTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"wizard_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _annotationService = new AnnotationService(_workspaceService);
        _waveformService = new WaveformService(_workspaceService);
        _imageService = new AyahImageService(_workspaceService);
        _compositionService = new CompositionService(_workspaceService, _imageService);

        var audioDir = Path.Combine(_testWorkspaceDir, "media", "audio", "sources");
        Directory.CreateDirectory(audioDir);
        _testAudioPath = Path.Combine(audioDir, "husary_001.mp3");
        File.WriteAllBytes(_testAudioPath, new byte[1024]);

        var imgDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "ayahs");
        Directory.CreateDirectory(imgDir);
        _testImage1 = Path.Combine(imgDir, "ayah_001_001.png");
        _testImage2 = Path.Combine(imgDir, "ayah_001_002.png");
        CreateDummyImage(_testImage1);
        CreateDummyImage(_testImage2);

        // Seed DB
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        db.Database.EnsureCreated();

        db.Reciters.Add(new ReciterEntity { Id = "saber_abdelhakam", NameArabic = "الشيخ صابر عبد الحكم", NameLatin = "Saber" });
        db.RecitationCollections.Add(new RecitationCollectionEntity
        {
            Id = "saber_abdelhakam_warsh",
            ReciterId = "saber_abdelhakam",
            TitleArabic = "المصحف المرتل برواية ورش"
        });

        db.AudioSources.Add(new AudioSourceEntity
        {
            Id = "saber_abdelhakam_warsh:001",
            CollectionId = "saber_abdelhakam_warsh",
            SurahNumber = 1,
            RelativeFilePath = Path.GetRelativePath(_testWorkspaceDir, _testAudioPath),
            DurationSeconds = 40.0,
            SampleRate = 44100,
            Channels = 2,
            FileSizeBytes = 1024
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

        db.Passages.AddRange(
            new AyahPassageEntity
            {
                AudioSourceId = "saber_abdelhakam_warsh:001",
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                StartTimeSec = 0.0,
                EndTimeSec = 2.0,
                Kind = PassageKind.Full,
                IsValidatedByUser = true,
                Revision = 1
            },
            new AyahPassageEntity
            {
                AudioSourceId = "saber_abdelhakam_warsh:001",
                AyahId = "1:2",
                SurahNumber = 1,
                AyahNumber = 2,
                StartTimeSec = 2.0,
                EndTimeSec = 4.0,
                Kind = PassageKind.Full,
                IsValidatedByUser = true,
                Revision = 1
            }
        );

        db.ImageRecipes.AddRange(
            new AyahImageRecipeEntity
            {
                AyahId = "1:1",
                SurahNumber = 1,
                AyahNumber = 1,
                PageNumber = 1,
                RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage1),
                Status = AyahImageStatus.Approved,
                Revision = 1
            },
            new AyahImageRecipeEntity
            {
                AyahId = "1:2",
                SurahNumber = 1,
                AyahNumber = 2,
                PageNumber = 1,
                RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage2),
                Status = AyahImageStatus.Approved,
                Revision = 1
            }
        );

        db.SaveChanges();
    }

    [Fact]
    public async Task RefreshWizardSequence_LoadsConsecutiveAyahsAndValidatesReadyState()
    {
        var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        vm.WizardStartAyah = 1;
        vm.WizardEndAyah = 2;

        await vm.RefreshWizardSequenceAsync();

        Assert.Equal(2, vm.WizardAyatItems.Count);
        Assert.True(vm.IsWizardSequenceValid);
        Assert.Contains("مسجلة وجاهزة", vm.WizardValidationSummary);
        Assert.True(vm.WizardAyatItems[0].IsFullyReady);
        Assert.True(vm.WizardAyatItems[1].IsFullyReady);
    }

    [Fact]
    public async Task RefreshWizardSequence_WhenAyahMissingImage_DetectsMissing()
    {
        // Remove image for ayah 2
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            var recipe = db.ImageRecipes.First(r => r.AyahId == "1:2");
            var fullPath = Path.Combine(_testWorkspaceDir, recipe.RelativeImagePath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            recipe.RelativeImagePath = "";
            db.SaveChanges();
        }

        var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        vm.WizardStartAyah = 1;
        vm.WizardEndAyah = 2;

        await vm.RefreshWizardSequenceAsync();

        Assert.Equal(2, vm.WizardAyatItems.Count);
        Assert.False(vm.IsWizardSequenceValid);
        Assert.False(vm.WizardAyatItems[1].IsFullyReady);
    }

    [Fact]
    public async Task SaveAndReopenProject_PersistsAndRestoresAllSettings()
    {
        var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        vm.ProjectTitle = "مشروع اختباري مميز";
        vm.WizardStartAyah = 1;
        vm.WizardEndAyah = 2;
        vm.WizardOrientation = VideoOrientation.Vertical;
        vm.WizardBackground = BackgroundType.Dark;
        vm.WizardWatermark = WatermarkType.Text;
        vm.WizardWatermarkText = "@قناة_القرآن";
        vm.WizardWatermarkPosition = WatermarkPosition.TopRight;
        vm.WizardWatermarkOpacity = 0.70;

        await vm.RefreshWizardSequenceAsync();
        await vm.SaveProjectAsync();

        Assert.NotNull(vm.ActiveProjectId);
        Assert.Single(vm.SavedProjects);

        // Reset VM state
        vm.ProjectTitle = "عنوان مختلف";
        vm.WizardOrientation = VideoOrientation.Horizontal;
        vm.WizardBackground = BackgroundType.Light;
        vm.WizardWatermarkText = "";

        // Reopen project
        var savedProject = vm.SavedProjects[0];
        await vm.OpenSavedProjectAsync(savedProject);

        Assert.Equal("مشروع اختباري مميز", vm.ProjectTitle);
        Assert.Equal(VideoOrientation.Vertical, vm.WizardOrientation);
        Assert.Equal(BackgroundType.Dark, vm.WizardBackground);
        Assert.Equal(WatermarkType.Text, vm.WizardWatermark);
        Assert.Equal("@قناة_القرآن", vm.WizardWatermarkText);
        Assert.Equal(WatermarkPosition.TopRight, vm.WizardWatermarkPosition);
        Assert.Equal(0.70, vm.WizardWatermarkOpacity);
    }

    [Fact]
    public async Task DeleteSavedProject_RemovesProjectWithoutTouchingOriginalMedia()
    {
        var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        vm.WizardStartAyah = 1;
        vm.WizardEndAyah = 2;
        await vm.RefreshWizardSequenceAsync();
        await vm.SaveProjectAsync();

        Assert.Single(vm.SavedProjects);
        var project = vm.SavedProjects[0];

        await vm.DeleteSavedProjectAsync(project);

        Assert.Empty(vm.SavedProjects);

        // Verify underlying database: passages & recipes are untouched
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        Assert.Equal(2, db.Passages.Count());
        Assert.Equal(2, db.ImageRecipes.Count());
        Assert.Empty(db.Projects);
    }

    [Fact]
    public void WizardModeToggle_ShouldSwitchModes()
    {
        using var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        Assert.False(vm.IsWizardMode);
        Assert.True(vm.IsFineTuningMode);

        vm.SelectWizardMode();
        Assert.True(vm.IsWizardMode);
        Assert.False(vm.IsFineTuningMode);

        vm.SelectFineTuningMode();
        Assert.False(vm.IsWizardMode);
        Assert.True(vm.IsFineTuningMode);

        vm.ToggleStudioMode();
        Assert.True(vm.IsWizardMode);
        Assert.False(vm.IsFineTuningMode);
    }

    [Fact]
    public async Task PreviewNavigation_ShouldUpdatePreviewIndexAndDescription()
    {
        using var vm = new StudioViewModel(
            _workspaceService,
            _annotationService,
            _waveformService,
            _imageService,
            _compositionService,
            surahNumber: 1,
            ayahNumber: 1);

        vm.WizardStartAyah = 1;
        vm.WizardEndAyah = 2;
        await vm.RefreshWizardSequenceAsync();

        Assert.Equal(2, vm.WizardAyatItems.Count);
        Assert.Equal(0, vm.PreviewAyahIndex);

        vm.PreviewNextAyah();
        Assert.Equal(1, vm.PreviewAyahIndex);

        // Should not exceed bounds
        vm.PreviewNextAyah();
        Assert.Equal(1, vm.PreviewAyahIndex);

        vm.PreviewPreviousAyah();
        Assert.Equal(0, vm.PreviewAyahIndex);

        // Should not go below 0
        vm.PreviewPreviousAyah();
        Assert.Equal(0, vm.PreviewAyahIndex);
    }

    private static void CreateDummyImage(string path)
    {
        using var bmp = new Bitmap(200, 50);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        bmp.Save(path, ImageFormat.Png);
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
