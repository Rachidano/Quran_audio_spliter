using System.IO;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class ShellViewModelTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;

    public ShellViewModelTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_shell_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
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
    public void MainViewModel_InitializesWithLibraryAsDefaultTab()
    {
        var mainVm = new MainViewModel(_workspaceService);

        Assert.Equal("Library", mainVm.ActiveTab);
        Assert.IsType<LibraryViewModel>(mainVm.CurrentContentViewModel);
        Assert.False(mainVm.IsSettingsOpen);
    }

    [Fact]
    public void MainViewModel_NavigationBetweenTheThreeMainTabs_SwitchesViewModelAndStatus()
    {
        var mainVm = new MainViewModel(_workspaceService);

        // Switch to Studio
        mainVm.SelectStudio();
        Assert.Equal("Studio", mainVm.ActiveTab);
        Assert.IsType<StudioViewModel>(mainVm.CurrentContentViewModel);

        // Switch to MyVideos
        mainVm.SelectMyVideos();
        Assert.Equal("MyVideos", mainVm.ActiveTab);
        Assert.IsType<MyVideosViewModel>(mainVm.CurrentContentViewModel);

        // Switch to BatchExport
        mainVm.SelectBatchExport();
        Assert.Equal("BatchExport", mainVm.ActiveTab);
        Assert.IsType<BatchExportViewModel>(mainVm.CurrentContentViewModel);

        // Switch back to Library
        mainVm.SelectLibrary();
        Assert.Equal("Library", mainVm.ActiveTab);
        Assert.IsType<LibraryViewModel>(mainVm.CurrentContentViewModel);
    }

    [Fact]
    public void LibraryViewModel_SearchFilter_FindsByArabicNameNumberAndLatin()
    {
        var libVm = new LibraryViewModel((s, a) => { });

        // Search by number "2"
        libVm.SearchQuery = "2";
        Assert.Contains(libVm.FilteredSurahs, s => s.Number == 2);

        // Search by Arabic name
        libVm.SearchQuery = "البقرة";
        Assert.Single(libVm.FilteredSurahs);
        Assert.Equal(2, libVm.FilteredSurahs[0].Number);

        // Search by Latin name
        libVm.SearchQuery = "Fatihah";
        Assert.Single(libVm.FilteredSurahs);
        Assert.Equal(1, libVm.FilteredSurahs[0].Number);
    }

    [Fact]
    public void StudioViewModel_NextAndPreviousAyah_TraversesAyahsAccurately()
    {
        var studioVm = new StudioViewModel(_workspaceService, surahNumber: 1, ayahNumber: 6);

        Assert.Equal(6, studioVm.CurrentAyahNumber);

        // Next -> Ayah 7
        studioVm.NextAyahCommand.Execute(null);
        Assert.Equal(7, studioVm.CurrentAyahNumber);

        // Next from last ayah of Fatiha (7) -> Surah 2 (Baqarah), Ayah 1
        studioVm.NextAyahCommand.Execute(null);
        Assert.Equal(2, studioVm.CurrentSurah.Number);
        Assert.Equal(1, studioVm.CurrentAyahNumber);

        // Previous -> Surah 1, Ayah 7
        studioVm.PreviousAyahCommand.Execute(null);
        Assert.Equal(1, studioVm.CurrentSurah.Number);
        Assert.Equal(7, studioVm.CurrentAyahNumber);
    }
}
