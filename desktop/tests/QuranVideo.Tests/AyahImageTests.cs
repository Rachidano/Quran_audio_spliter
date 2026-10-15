using System.Drawing;
using System.IO;
using System.Text.Json;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class AyahImageTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly string _testPagePath;

    public AyahImageTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_image_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        // Copy or create test page image in workspace
        var mushafDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "pages");
        Directory.CreateDirectory(mushafDir);
        _testPagePath = Path.Combine(mushafDir, "page-001.png");

        var sourceRealPage = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full/page-001.png");
        if (File.Exists(sourceRealPage))
        {
            File.Copy(sourceRealPage, _testPagePath, overwrite: true);
        }
        else
        {
            using var bmp = new Bitmap(1000, 1500);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Wheat);
            bmp.Save(_testPagePath);
        }

        // Initialize SQLite schema & seed test data
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.Database.EnsureCreated();
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

        _imageService = new AyahImageService(_workspaceService);
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
        }
    }

    [Fact]
    public async Task GetOrExtractAyahImage_ExtractsLosslessPngAndGeneratesManifest()
    {
        var (success, imagePath, recipe) = await _imageService.GetOrExtractAyahImageAsync(1, 1);

        Assert.True(success);
        Assert.True(File.Exists(imagePath));
        Assert.True(recipe.Width > 0);
        Assert.True(recipe.Height > 0);
        Assert.Equal(AyahImageStatus.Suggested, recipe.Status);
        Assert.Equal(1, recipe.Revision);
        Assert.False(string.IsNullOrEmpty(recipe.ImageSha256));

        // Sidecar manifest check (L07-06)
        var manifestPath = Path.Combine(_testWorkspaceDir, "media", "images", "ayahs", "ayah_001_001_manifest.json");
        Assert.True(File.Exists(manifestPath));

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<ImageRecipeManifest>(manifestJson);
        Assert.NotNull(manifest);
        Assert.Equal("1:1", manifest.AyahId);
        Assert.Equal(recipe.ImageSha256, manifest.ImageSha256);
    }

    [Fact]
    public async Task GetOrExtractAyahImage_CachedCall_ReusesExistingFileWithoutRecomputation()
    {
        var (_, firstPath, firstRecipe) = await _imageService.GetOrExtractAyahImageAsync(1, 1);
        var firstWriteTime = File.GetLastWriteTimeUtc(firstPath);

        // Immediate second call with forceRecompute: false
        var (_, secondPath, secondRecipe) = await _imageService.GetOrExtractAyahImageAsync(1, 1, forceRecompute: false);

        Assert.Equal(firstPath, secondPath);
        Assert.Equal(firstRecipe.ImageSha256, secondRecipe.ImageSha256);
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(secondPath));
    }

    [Fact]
    public async Task ApproveImage_UpdatesStatusToApproved()
    {
        await _imageService.GetOrExtractAyahImageAsync(1, 1);

        var ok = await _imageService.ApproveImageAsync(1, 1);
        Assert.True(ok);

        var recipe = await _imageService.GetRecipeAsync("1:1");
        Assert.NotNull(recipe);
        Assert.Equal(AyahImageStatus.Approved, recipe.Status);
    }

    [Fact]
    public async Task ApplyCorrection_NudgesMarginsAndIncrementsRevision()
    {
        var (_, origPath, origRecipe) = await _imageService.GetOrExtractAyahImageAsync(1, 1);
        Assert.Equal(1, origRecipe.Revision);
        var origWidth = origRecipe.Width;

        // Apply +10px left and +10px right
        var (success, newPath, updatedRecipe) = await _imageService.ApplyCorrectionAsync(
            1, 1, deltaTop: 0, deltaBottom: 0, deltaLeft: 10, deltaRight: 10);

        Assert.True(success);
        Assert.Equal(2, updatedRecipe.Revision);
        Assert.Equal(AyahImageStatus.Corrected, updatedRecipe.Status);
        Assert.Equal(10, updatedRecipe.MarginLeft);
        Assert.Equal(10, updatedRecipe.MarginRight);
        Assert.True(updatedRecipe.Width > origWidth);
    }

    [Fact]
    public async Task ResetCorrection_ResetsMarginsToZero()
    {
        await _imageService.ApplyCorrectionAsync(1, 1, deltaTop: 15, deltaBottom: 15, deltaLeft: 15, deltaRight: 15);

        var (success, _, resetRecipe) = await _imageService.ResetCorrectionAsync(1, 1);

        Assert.True(success);
        Assert.Equal(0, resetRecipe.MarginTop);
        Assert.Equal(0, resetRecipe.MarginBottom);
        Assert.Equal(0, resetRecipe.MarginLeft);
        Assert.Equal(0, resetRecipe.MarginRight);
        Assert.Equal(AyahImageStatus.Suggested, resetRecipe.Status);
    }

    [Fact]
    public async Task GenerateSideBySideComparison_ProducesValid3ViewImage()
    {
        var compPath = await _imageService.GenerateSideBySideComparisonAsync(1, 1);

        Assert.True(File.Exists(compPath));
        using var bmp = new Bitmap(compPath);
        Assert.True(bmp.Width > 1500); // 3 panels side-by-side
        Assert.True(bmp.Height > 800);
    }

    [Fact]
    public async Task StudioViewModel_ImageIntegration_ApprovesAndNudges()
    {
        var vm = new StudioViewModel(_workspaceService, imageService: _imageService, surahNumber: 1, ayahNumber: 1);
        await vm.LoadAyahImageAsync();

        Assert.NotNull(vm.CurrentIsolatedImagePath);
        Assert.Equal(AyahImageStatus.Suggested, vm.CurrentImageStatus);

        // Nudge
        await vm.NudgeMarginCommand.ExecuteAsync("top+5");
        Assert.Equal(5, vm.MarginTop);
        Assert.Equal(AyahImageStatus.Corrected, vm.CurrentImageStatus);
    }

    [Fact]
    public void StudioViewModel_AdjustCommands_AcceptStringParametersFromXaml()
    {
        var vm = new StudioViewModel(_workspaceService, imageService: _imageService, surahNumber: 1, ayahNumber: 1);
        vm.StartTimeSec = 5.0;
        vm.EndTimeSec = 10.0;

        // String parameter as passed by WPF XAML CommandParameter="-0.1" / "0.1"
        vm.AdjustStartCommand.Execute("-0.1");
        Assert.Equal(4.9, vm.StartTimeSec);

        vm.AdjustStartCommand.Execute("0.1");
        Assert.Equal(5.0, vm.StartTimeSec);

        vm.AdjustEndCommand.Execute("-0.1");
        Assert.Equal(9.9, vm.EndTimeSec);

        vm.AdjustEndCommand.Execute("0.1");
        Assert.Equal(10.0, vm.EndTimeSec);
    }

    [Fact]
    public void StudioViewModel_StepCommands_ParameterlessExecute()
    {
        var vm = new StudioViewModel(_workspaceService, imageService: _imageService, surahNumber: 1, ayahNumber: 1);
        // Parameterless commands as bound in XAML without CommandParameter
        vm.StepForwardCommand.Execute(null);
        vm.StepBackwardCommand.Execute(null);
        Assert.NotNull(vm.StepForwardCommand);
        Assert.NotNull(vm.StepBackwardCommand);
    }
}
