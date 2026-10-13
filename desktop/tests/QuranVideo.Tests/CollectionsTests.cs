using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class CollectionsTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AyahImageService _imageService;
    private readonly CompositionService _compositionService;
    private readonly SequenceValidationService _validationService;
    private readonly string _testAudioPath;
    private readonly string _testImage1;

    public CollectionsTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"collections_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _imageService = new AyahImageService(_workspaceService);
        _compositionService = new CompositionService(_workspaceService, _imageService);
        _validationService = new SequenceValidationService(_workspaceService);

        var audioDir = Path.Combine(_testWorkspaceDir, "media", "audio", "sources");
        Directory.CreateDirectory(audioDir);
        _testAudioPath = Path.Combine(audioDir, "saber_001.wav");
        using (var writer = new NAudio.Wave.WaveFileWriter(_testAudioPath, new NAudio.Wave.WaveFormat(44100, 16, 1)))
        {
            for (int i = 0; i < 44100 * 2; i++) writer.WriteSample(0f);
        }

        var imgDir = Path.Combine(_testWorkspaceDir, "media", "mushaf", "ayahs");
        Directory.CreateDirectory(imgDir);
        _testImage1 = Path.Combine(imgDir, "ayah_001_001.png");
        CreateDummyImage(_testImage1);

        // Seed DB
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        db.Database.EnsureCreated();

        db.Reciters.Add(new ReciterEntity { Id = "saber_abdelhakam", NameArabic = "الشيخ صابر الحكم", NameLatin = "Saber" });
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

        db.Passages.Add(new AyahPassageEntity
        {
            Id = 1,
            AudioSourceId = "saber_abdelhakam_warsh:001",
            AyahId = "1:1",
            SurahNumber = 1,
            AyahNumber = 1,
            StartTimeSec = 0.0,
            EndTimeSec = 2.0,
            Kind = PassageKind.Full,
            IsValidatedByUser = true
        });

        db.ImageRecipes.Add(new AyahImageRecipeEntity
        {
            Id = 1,
            AyahId = "1:1",
            SurahNumber = 1,
            AyahNumber = 1,
            PageNumber = 1,
            RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage1),
            Status = AyahImageStatus.Approved
        });

        // Add 2 test projects
        var p1 = new VideoProjectEntity
        {
            Id = 1,
            Title = "سورة الفاتحة - الآية 1",
            SurahNumber = 1,
            StartAyahNumber = 1,
            EndAyahNumber = 1,
            ReciterId = "saber_abdelhakam",
            AudioSourceId = "saber_abdelhakam_warsh:001",
            Orientation = "Horizontal",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        p1.SelectedAyahs.Add(new ProjectAyahEntity
        {
            OrderIndex = 1,
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            PassageId = 1,
            StartTimeSec = 0.0,
            EndTimeSec = 2.0,
            RelativeImagePath = Path.GetRelativePath(_testWorkspaceDir, _testImage1)
        });

        var p2 = new VideoProjectEntity
        {
            Id = 2,
            Title = "مشروع غير مكتمل",
            SurahNumber = 1,
            StartAyahNumber = 2,
            EndAyahNumber = 2,
            Orientation = "Horizontal",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        // p2 has no passages or images (unready)

        db.Projects.AddRange(p1, p2);
        db.SaveChanges();
    }

    [Fact]
    public async Task CreateRenameDeleteCollection_Lifecycle_ShouldManageCollections()
    {
        var vm = new CollectionsViewModel(_workspaceService, _compositionService, _validationService);
        await vm.LoadCollectionsAsync();

        Assert.Empty(vm.Collections);

        // 1. Create collection
        vm.StartCreateCollection();
        vm.NewCollectionName = "أذكار الصباح";
        vm.NewCollectionDescription = "مجموعة مباركة";
        await vm.ConfirmCreateCollectionAsync();

        Assert.Single(vm.Collections);
        Assert.Equal("أذكار الصباح", vm.Collections[0].Name);

        // 2. Rename collection
        vm.SelectedCollection = vm.Collections[0];
        vm.StartRenameCollection();
        vm.RenamedCollectionName = "أذكار الصباح والمساء";
        await vm.ConfirmRenameCollectionAsync();

        Assert.Equal("أذكار الصباح والمساء", vm.SelectedCollection!.Name);

        // 3. Delete collection
        await vm.DeleteCollectionAsync();
        Assert.Empty(vm.Collections);

        // Underlying projects and passages must remain 100% intact
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        Assert.Equal(2, db.Projects.Count());
        Assert.Single(db.Passages);
    }

    [Fact]
    public async Task AddItem_MoveItems_RemoveItem_PreservesProjects()
    {
        var vm = new CollectionsViewModel(_workspaceService, _compositionService, _validationService);
        await vm.LoadCollectionsAsync();

        // Create collection
        vm.NewCollectionName = "قصار السور";
        await vm.ConfirmCreateCollectionAsync();
        Assert.NotNull(vm.SelectedCollection);

        // Available projects should have 2 items
        Assert.Equal(2, vm.AvailableProjectsToAdd.Count);

        // Add p1
        vm.SelectedProjectToAdd = vm.AvailableProjectsToAdd.First(p => p.Id == 1);
        await vm.AddProjectToCollectionAsync();

        Assert.Single(vm.CurrentCollectionItems);
        Assert.Equal("سورة الفاتحة - الآية 1", vm.CurrentCollectionItems[0].Title);
        Assert.True(vm.CurrentCollectionItems[0].IsReady);

        // Add p2
        vm.SelectedProjectToAdd = vm.AvailableProjectsToAdd.First(p => p.Id == 2);
        await vm.AddProjectToCollectionAsync();

        Assert.Equal(2, vm.CurrentCollectionItems.Count);

        // Reorder (Move p2 up)
        var item2 = vm.CurrentCollectionItems[1];
        await vm.MoveItemUpAsync(item2);
        Assert.Equal(item2.ItemId, vm.CurrentCollectionItems[0].ItemId);

        // Remove item from collection
        await vm.RemoveItemFromCollectionAsync(vm.CurrentCollectionItems[0]);
        Assert.Single(vm.CurrentCollectionItems);

        // Verify underlying database: projects are still there
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        Assert.Equal(2, db.Projects.Count());
    }

    [Fact]
    public async Task ExportCollectionBatch_WithUnreadyItems_ShouldBlockWholeBatch()
    {
        var vm = new CollectionsViewModel(_workspaceService, _compositionService, _validationService);
        await vm.LoadCollectionsAsync();

        // Create collection and add unready p2
        vm.NewCollectionName = "مجموعة مختلطة";
        await vm.ConfirmCreateCollectionAsync();

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        var p2 = db.Projects.First(p => p.Id == 2);

        vm.SelectedProjectToAdd = p2;
        await vm.AddProjectToCollectionAsync();

        Assert.Single(vm.CurrentCollectionItems);
        Assert.False(vm.CurrentCollectionItems[0].IsReady);

        // Try batch export: must block!
        await vm.ExportCollectionBatchAsync();

        Assert.False(vm.IsExportingBatch);
        Assert.NotNull(vm.BatchErrorMessage);
        Assert.Contains("تحتوي المجموعة على عناصر غير جاهزة", vm.BatchErrorMessage);
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
