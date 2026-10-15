using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;

    [ObservableProperty]
    private string _activeTab = "Library"; // "Library", "Studio", "MyVideos"

    [ObservableProperty]
    private ViewModelBase? _currentContentViewModel;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private string _statusText = "جاهز";

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    public LibraryViewModel LibraryVM { get; }
    public StudioViewModel StudioVM { get; }
    public BatchExportViewModel BatchExportVM { get; }
    public CollectionsViewModel CollectionsVM { get; }
    public MyVideosViewModel MyVideosVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public QuranSearchService SearchService { get; } = new();

    public MainViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        WorkspacePath = _workspaceService.WorkspacePath;

        LibraryVM = new LibraryViewModel(NavigateToStudioWithTarget, SearchService, _workspaceService);
        StudioVM = new StudioViewModel(_workspaceService);
        BatchExportVM = new BatchExportViewModel(_workspaceService);
        CollectionsVM = new CollectionsViewModel(_workspaceService);
        MyVideosVM = new MyVideosViewModel(_workspaceService);
        SettingsVM = new SettingsViewModel(_workspaceService, () => { IsSettingsOpen = false; _ = StudioVM.RefreshRecitersAsync(selectDefault: true); });

        CurrentContentViewModel = LibraryVM;

        StudioVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(StudioVM.IsFineTuningMode) or nameof(StudioVM.IsWizardMode))
            {
                OnPropertyChanged(nameof(IsAudioStudioTabActive));
                OnPropertyChanged(nameof(IsVideoStudioTabActive));
            }
        };
    }

    public async Task InitializeSessionAsync()
    {
        var dbPath = _workspaceService.GetDatabasePath();

        // 0. Ensure database schema is migrated and up to date (V3)
        var migrationService = new DatabaseMigrationService();
        await migrationService.MigrateDatabaseAsync(dbPath);

        // 1. Initialize reciter collections (Saber Abdelhakam default, Al-Husary)
        var reciterService = new RecitationLibraryService(_workspaceService);
        await reciterService.InitializeDefaultCollectionAsync();
        await StudioVM.RefreshRecitersAsync(selectDefault: true);

        // 2. Auto-detect Mushaf pack on fresh startup if no edition exists yet
        try
        {
            using var db = new AppDbContext(dbPath);
            if (!await db.Editions.AnyAsync() || !await db.Regions.AnyAsync())
            {
                var candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pack"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mushaf"),
                    Path.Combine(_workspaceService.WorkspacePath, "pack"),
                    Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\data\mushaf\editions\marw-newMushaf\20260913-full")
                };

                string? foundPack = null;
                foreach (var cand in candidates)
                {
                    if (Directory.Exists(cand) && File.Exists(Path.Combine(cand, "manifest.json")))
                    {
                        foundPack = cand;
                        break;
                    }
                }

                if (foundPack != null)
                {
                    StatusText = "جارٍ استيراد حزمة المصحف الشريف لأول مرة...";
                    var importService = new CorpusImportService(_workspaceService);
                    await importService.ImportPackAsync(foundPack);
                    StatusText = "تم استيراد حزمة المصحف الشريف بنجاح";
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Impossible d'importer automatiquement le pack: {ex.Message}");
        }

        // 3. Initialize search index
        await SearchService.InitializeAsync(dbPath);

        // 4. Restore session
        var session = await _workspaceService.LoadSessionStateAsync();
        await LibraryVM.RefreshSurahStatusAsync();
        if (session.LastSurahNumber > 0 && session.LastAyahNumber > 0)
        {
            StudioVM.SetTarget(session.LastSurahNumber, session.LastAyahNumber);
            StudioVM.IsHorizontal = session.LastOrientation == "Horizontal";
            await StudioVM.LoadAyahImageAsync();
        }

        // Par défaut au lancement, le premier écran doit toujours être la bibliothèque
        SelectLibrary();
    }

    private void NavigateToStudioWithTarget(int surahNumber, int ayahNumber)
    {
        // Le studio s'ouvre avec le récitateur choisi dans la bibliothèque
        if (LibraryVM.SelectedReciter is { } reciter) StudioVM.SelectRecitation(reciter.CollectionId);
        StudioVM.SetTarget(surahNumber, ayahNumber);
        StudioVM.IsFineTuningMode = true;
        SelectStudio();
    }

    public bool IsAudioStudioTabActive => ActiveTab == "AudioStudio" || (ActiveTab == "Studio" && StudioVM.IsFineTuningMode);
    public bool IsVideoStudioTabActive => ActiveTab == "VideoStudio" || (ActiveTab == "Studio" && !StudioVM.IsFineTuningMode);

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsAudioStudioTabActive));
        OnPropertyChanged(nameof(IsVideoStudioTabActive));
    }

    [RelayCommand]
    public void SelectLibrary()
    {
        ActiveTab = "Library";
        CurrentContentViewModel = LibraryVM;
        StatusText = "المكتبة — استعراض السور والآيات";
        _ = LibraryVM.RefreshSurahStatusAsync();
        SaveSession();
    }

    [RelayCommand]
    public void SelectAudioStudio()
    {
        StudioVM.IsFineTuningMode = true;
        SelectStudio();
    }

    [RelayCommand]
    public void SelectVideoStudio()
    {
        StudioVM.IsFineTuningMode = false;
        SelectStudio();
    }

    [RelayCommand]
    public void SelectStudio()
    {
        ActiveTab = "Studio";
        CurrentContentViewModel = StudioVM;
        _ = StudioVM.RefreshRecitersAsync();
        StatusText = StudioVM.IsFineTuningMode 
            ? $"استوديو التقطيع — {StudioVM.TargetDescription}" 
            : $"إعداد الفيديو — {StudioVM.TargetDescription}";
        _ = StudioVM.LoadAyahImageAsync();
        SaveSession();
    }

    [RelayCommand]
    public void SelectCollections()
    {
        ActiveTab = "Collections";
        _ = CollectionsVM.LoadCollectionsAsync();
        CurrentContentViewModel = CollectionsVM;
        StatusText = "مجموعاتي — تنظيم وتصدير السلاسل الموضوعية";
        SaveSession();
    }

    [RelayCommand]
    public void SelectBatchExport()
    {
        ActiveTab = "BatchExport";
        _ = BatchExportVM.LoadJobsAsync();
        CurrentContentViewModel = BatchExportVM;
        StatusText = "التصدير بالدفعة — إدارة قائمة الانتظار";
        SaveSession();
    }

    [RelayCommand]
    public void SelectMyVideos()
    {
        ActiveTab = "MyVideos";
        MyVideosVM.RefreshVideos();
        CurrentContentViewModel = MyVideosVM;
        StatusText = "فيديوهاتي — استعراض المقاطع المنتجة";
        SaveSession();
    }

    [RelayCommand]
    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    private void SaveSession()
    {
        _ = _workspaceService.SaveSessionStateAsync(new SessionState
        {
            ActiveView = ActiveTab,
            LastSurahNumber = StudioVM.CurrentSurah.Number,
            LastAyahNumber = StudioVM.CurrentAyahNumber,
            LastOrientation = StudioVM.IsHorizontal ? "Horizontal" : "Vertical"
        });
    }
}
