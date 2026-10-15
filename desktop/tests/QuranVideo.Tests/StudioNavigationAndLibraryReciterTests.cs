using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Choix de la sourate dans le studio, choix du récitateur dans la bibliothèque,
/// et récitateur transmis au studio.
/// </summary>
public class StudioNavigationAndLibraryReciterTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;

    public StudioNavigationAndLibraryReciterTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"nav_reciter_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        new RecitationCatalogService(_workspaceService).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Fact]
    public void StudioSurahSelector_OpensAyahOneOfChosenSurah()
    {
        using var vm = new StudioViewModel(_workspaceService, surahNumber: 1, ayahNumber: 7);
        Assert.Equal(7, vm.AyahNumbers.Count);

        vm.StudioSurahSelection = SurahCatalogService.GetSurah(2)!;

        Assert.Equal(2, vm.CurrentSurah.Number);
        Assert.Equal(1, vm.CurrentAyahNumber);
        Assert.Equal(285, vm.AyahNumbers.Count);   // compte Warsh
        Assert.Equal(2, vm.StudioSurahSelection.Number);
    }

    [Fact]
    public void StudioAyahSelector_UsesCurrentAyahNumber()
    {
        using var vm = new StudioViewModel(_workspaceService, surahNumber: 2, ayahNumber: 1);

        vm.CurrentAyahNumber = 255;

        Assert.Equal("سورة البقرة — الآية 255", vm.TargetDescription);
    }

    [Fact]
    public void Studio_SelectRecitation_SwitchesToRequestedReciter()
    {
        using var vm = new StudioViewModel(_workspaceService);
        var saber = new RecitationChoice(RecitationCatalogService.SaberCollection, "saber_abdelhakam", "الشيخ صابر عبد الحكم");
        var husary = new RecitationChoice(RecitationCatalogService.HusaryCollection, "husary", "الشيخ محمود خليل الحصري (ورش عن نافع)");
        vm.Reciters.Add(saber);
        vm.Reciters.Add(husary);
        vm.SelectedRecitation = saber;

        vm.SelectRecitation(RecitationCatalogService.HusaryCollection);

        Assert.Equal(husary, vm.SelectedRecitation);
        Assert.Equal(RecitationCatalogService.HusaryCollection, vm.CurrentCollectionId);
    }

    [Fact]
    public async Task Library_ListsReciters_AndCountsPreparedAyatPerReciter()
    {
        using (var db = new AppDbContext(_workspaceService.GetDatabasePath()))
        {
            db.Surahs.Add(new SurahEntity { Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 7, StartPage = 1, EndPage = 1 });
            for (int a = 1; a <= 7; a++)
                db.Ayahs.Add(new AyahEntity { Id = $"1:{a}", SurahNumber = 1, AyahNumber = a, VerseNo = a, PageNumber = 1 });
            foreach (var collection in new[] { RecitationCatalogService.SaberCollection, RecitationCatalogService.HusaryCollection })
                db.AudioSources.Add(new AudioSourceEntity { Id = $"{collection}:001", CollectionId = collection, SurahNumber = 1, RelativeFilePath = "x.mp3" });
            // Saber : 3 ayat préparées ; Husary : 5 ayat préparées
            for (int a = 1; a <= 5; a++)
            {
                if (a <= 3)
                    db.Passages.Add(new AyahPassageEntity { AudioSourceId = $"{RecitationCatalogService.SaberCollection}:001", SurahNumber = 1, AyahNumber = a, AyahId = $"1:{a}", StartTimeSec = a, EndTimeSec = a + 1 });
                db.Passages.Add(new AyahPassageEntity { AudioSourceId = $"{RecitationCatalogService.HusaryCollection}:001", SurahNumber = 1, AyahNumber = a, AyahId = $"1:{a}", StartTimeSec = a, EndTimeSec = a + 1 });
            }
            db.SaveChanges();
        }

        var library = new LibraryViewModel((_, _) => { }, workspaceService: _workspaceService);
        await library.RefreshSurahStatusAsync();

        // Récitateur par défaut (Saber) choisi au premier affichage, tous les récitateurs proposés
        Assert.Equal(RecitationCatalogService.SaberCollection, library.SelectedReciter?.CollectionId);
        Assert.Contains(library.Reciters, r => r.CollectionId == RecitationCatalogService.HusaryCollection);
        Assert.Equal(3, library.SurahItems.Single(i => i.Number == 1).PreparedAyahsCount);

        library.SelectedReciter = library.Reciters.Single(r => r.CollectionId == RecitationCatalogService.HusaryCollection);
        await library.RefreshSurahStatusAsync();

        Assert.Equal(RecitationCatalogService.HusaryCollection, library.SelectedReciter?.CollectionId);
        Assert.Equal(5, library.SurahItems.Single(i => i.Number == 1).PreparedAyahsCount);
        Assert.Equal("الشيخ محمود خليل الحصري (ورش عن نافع)", library.CurrentReciterName);
    }
}
