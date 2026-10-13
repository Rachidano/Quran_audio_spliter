using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly Action<int, int> _onNavigateToStudio;
    private readonly QuranSearchService _searchService;
    private readonly WorkspaceService? _workspaceService;
    private readonly Dictionary<int, (int PreparedCount, bool IsAudioDownloaded, string StatusText, string Duration)> _surahRealStatus = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private SurahInfo? _selectedSurah;

    [ObservableProperty]
    private int _selectedAyahNumber = 1;

    [ObservableProperty]
    private bool _isSearchMode;

    [ObservableProperty]
    private string? _searchValidationMessage;

    public bool HasSearchValidationMessage => !string.IsNullOrWhiteSpace(SearchValidationMessage);

    partial void OnSearchValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSearchValidationMessage));
    }

    [ObservableProperty]
    private int _searchCurrentPage = 1;

    [ObservableProperty]
    private int _searchTotalPages = 1;

    [ObservableProperty]
    private int _searchTotalCount = 0;

    [ObservableProperty]
    private AyahSearchEntry? _selectedSearchResult;

    [ObservableProperty]
    private bool _hasLastWorkedAyah;

    [ObservableProperty]
    private int _lastWorkedSurahNumber;

    [ObservableProperty]
    private int _lastWorkedAyahNumber;

    [ObservableProperty]
    private string _lastWorkedDescription = string.Empty;

    [ObservableProperty]
    private string _lastWorkedAudioTime = string.Empty;

    [ObservableProperty]
    private string _currentReciterName = "الشيخ صابر عبد الحكم";

    [ObservableProperty]
    private SurahItemViewModel? _selectedSurahItem;

    public ObservableCollection<SurahInfo> FilteredSurahs { get; } = new();
    public ObservableCollection<AyahSearchEntry> SearchResults { get; } = new();
    public ObservableCollection<SurahItemViewModel> SurahItems { get; } = new();

    public LibraryViewModel(
        Action<int, int> onNavigateToStudio,
        QuranSearchService? searchService = null,
        WorkspaceService? workspaceService = null)
    {
        _onNavigateToStudio = onNavigateToStudio;
        _searchService = searchService ?? new QuranSearchService();
        _workspaceService = workspaceService;

        ApplyFilter();
        SelectedSurah = FilteredSurahs.FirstOrDefault();
    }

    partial void OnSearchQueryChanged(string value)
    {
        SearchCurrentPage = 1;
        ApplyFilter();
    }

    partial void OnSelectedSurahChanged(SurahInfo? value)
    {
        SelectedAyahNumber = 1;
    }

    private SurahItemViewModel CreateSurahItem(SurahInfo info)
    {
        if (_surahRealStatus.TryGetValue(info.Number, out var st))
        {
            return new SurahItemViewModel(info, st.PreparedCount, st.Duration, st.IsAudioDownloaded, st.StatusText);
        }
        return new SurahItemViewModel(info, 0, "—", false, "غير متوفر");
    }

    private void ApplyFilter()
    {
        var query = SearchQuery.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearchMode = false;
            SearchValidationMessage = null;
            SearchResults.Clear();
            LoadAllSurahs();
            return;
        }

        // Check if query is a reference or full text search
        bool isRef = QuranSearchService.TryParseReference(query, out _, out _);
        bool hasMinLetters = ArabicTextNormalizer.HasMinimumLetters(query);

        if (_searchService.IsInitialized && (isRef || hasMinLetters))
        {
            IsSearchMode = true;
            ExecuteSearch();
            return;
        }

        // If not enough letters for text search and not a reference:
        // Filter the surah list directly by name/number
        IsSearchMode = false;
        SearchResults.Clear();
        FilteredSurahs.Clear();

        if (!hasMinLetters && !isRef && query.Length > 0 && query.Length < 3 && !int.TryParse(query, out _))
        {
            SearchValidationMessage = "أدخل 3 أحرف على الأقل للبحث في نص الآيات، أو اكتب رقم الآية أو السورة مباشرة.";
        }
        else
        {
            SearchValidationMessage = null;
        }

        var all = SurahCatalogService.GetAllSurahs();
        SurahItems.Clear();
        foreach (var s in all)
        {
            if (s.Number.ToString().Contains(query) ||
                s.NameArabic.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.NameLatin.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSurahs.Add(s);
                SurahItems.Add(CreateSurahItem(s));
            }
        }

        if (SelectedSurah == null || !FilteredSurahs.Contains(SelectedSurah))
        {
            SelectedSurah = FilteredSurahs.FirstOrDefault();
            SelectedSurahItem = SurahItems.FirstOrDefault();
        }
    }

    private void LoadAllSurahs()
    {
        FilteredSurahs.Clear();
        SurahItems.Clear();
        foreach (var s in SurahCatalogService.GetAllSurahs())
        {
            FilteredSurahs.Add(s);
            SurahItems.Add(CreateSurahItem(s));
        }
        if (SelectedSurah == null || !FilteredSurahs.Contains(SelectedSurah))
        {
            SelectedSurah = FilteredSurahs.FirstOrDefault();
            SelectedSurahItem = SurahItems.FirstOrDefault();
        }
    }

    public async Task RefreshSurahStatusAsync()
    {
        if (_workspaceService == null) return;
        var dbPath = _workspaceService.GetDatabasePath();
        if (!File.Exists(dbPath)) return;

        try
        {
            using var db = new AppDbContext(dbPath);

            // 1. Reciter
            var defaultCollection = await db.RecitationCollections.AsNoTracking().FirstOrDefaultAsync();
            var collectionId = defaultCollection?.Id ?? "saber";
            if (!string.IsNullOrWhiteSpace(defaultCollection?.TitleArabic))
            {
                CurrentReciterName = defaultCollection.TitleArabic;
            }

            // 2. Audio sources
            var audioSources = await db.AudioSources.AsNoTracking()
                .Where(a => a.CollectionId == collectionId)
                .ToListAsync();
            var audioDict = audioSources.ToDictionary(a => a.SurahNumber);

            // 3. Prepared ayahs counts (distinct ayah passages)
            var passagesGrouped = await db.Passages.AsNoTracking()
                .GroupBy(p => p.SurahNumber)
                .Select(g => new { SurahNumber = g.Key, Prepared = g.Select(p => p.AyahNumber).Distinct().Count() })
                .ToListAsync();
            var prepDict = passagesGrouped.ToDictionary(x => x.SurahNumber, x => x.Prepared);

            // 4. Update dictionary cache
            _surahRealStatus.Clear();
            foreach (var s in SurahCatalogService.GetAllSurahs())
            {
                int prep = prepDict.GetValueOrDefault(s.Number, 0);
                bool isAudio = false;
                string statusText = "غير متوفر";
                string duration = "—";

                if (audioDict.TryGetValue(s.Number, out var audio))
                {
                    var audioFull = Path.Combine(_workspaceService.WorkspacePath, audio.RelativeFilePath);
                    if (File.Exists(audioFull))
                    {
                        isAudio = true;
                        statusText = "تم التنزيل";
                        var ts = TimeSpan.FromSeconds(audio.DurationSeconds);
                        duration = ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
                    }
                }

                _surahRealStatus[s.Number] = (prep, isAudio, statusText, duration);
            }

            // 5. Apply to currently loaded items
            foreach (var item in SurahItems)
            {
                if (_surahRealStatus.TryGetValue(item.Number, out var st))
                {
                    item.PreparedAyahsCount = st.PreparedCount;
                    item.IsAudioDownloaded = st.IsAudioDownloaded;
                    item.AudioStatusText = st.StatusText;
                    item.AudioDurationText = st.Duration;
                }
            }

            // 6. Real last worked passage
            var lastPassage = await db.Passages.AsNoTracking()
                .OrderByDescending(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (lastPassage != null)
            {
                SetLastWorkedAyah(lastPassage.SurahNumber, lastPassage.AyahNumber, lastPassage.StartTimeSec, lastPassage.EndTimeSec);
            }
            else
            {
                HasLastWorkedAyah = false;
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Erreur actualisation statut bibliothèque", ex);
        }
    }

    private void ExecuteSearch()
    {
        var result = _searchService.Search(SearchQuery, SearchCurrentPage, pageSize: 15);
        SearchValidationMessage = result.ValidationMessage;
        SearchTotalCount = result.TotalResults;
        SearchTotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalResults / 15.0));

        SearchResults.Clear();
        foreach (var item in result.Items)
        {
            SearchResults.Add(item);
        }
        SelectedSearchResult = SearchResults.FirstOrDefault();
    }

    [RelayCommand]
    private void NextSearchPage()
    {
        if (SearchCurrentPage < SearchTotalPages)
        {
            SearchCurrentPage++;
            ExecuteSearch();
        }
    }

    [RelayCommand]
    private void PreviousSearchPage()
    {
        if (SearchCurrentPage > 1)
        {
            SearchCurrentPage--;
            ExecuteSearch();
        }
    }

    public void SetLastWorkedAyah(int surahNumber, int ayahNumber, double startSec = 0, double endSec = 0)
    {
        if (surahNumber <= 0 || ayahNumber <= 0)
        {
            HasLastWorkedAyah = false;
            return;
        }

        var surah = SurahCatalogService.GetSurah(surahNumber);
        if (surah == null)
        {
            HasLastWorkedAyah = false;
            return;
        }

        LastWorkedSurahNumber = surahNumber;
        LastWorkedAyahNumber = ayahNumber;
        LastWorkedDescription = $"{surah.NameArabic} — الآية {ayahNumber}";
        if (endSec > startSec)
        {
            var startTs = TimeSpan.FromSeconds(startSec);
            var endTs = TimeSpan.FromSeconds(endSec);
            LastWorkedAudioTime = $"{startTs:mm\\:ss\\.ff} / {endTs:mm\\:ss\\.ff}";
        }
        else
        {
            LastWorkedAudioTime = "00:00";
        }
        HasLastWorkedAyah = true;
    }

    [RelayCommand]
    private void ResumeLastWork()
    {
        if (HasLastWorkedAyah && LastWorkedSurahNumber > 0 && LastWorkedAyahNumber > 0)
        {
            _onNavigateToStudio(LastWorkedSurahNumber, LastWorkedAyahNumber);
        }
    }

    [RelayCommand]
    private void OpenSurah(SurahItemViewModel? item)
    {
        if (item != null)
        {
            SelectedSurah = item.Info;
            SelectedSurahItem = item;
            _onNavigateToStudio(item.Number, item.HasWorkStarted ? item.PreparedAyahsCount : 1);
        }
        else if (SelectedSurah != null)
        {
            _onNavigateToStudio(SelectedSurah.Number, SelectedAyahNumber);
        }
    }

    [RelayCommand]
    private void OpenInStudio()
    {
        if (IsSearchMode && SelectedSearchResult != null)
        {
            _onNavigateToStudio(SelectedSearchResult.SurahNumber, SelectedSearchResult.AyahNumber);
        }
        else if (SelectedSurahItem != null)
        {
            _onNavigateToStudio(SelectedSurahItem.Number, SelectedSurahItem.HasWorkStarted ? SelectedSurahItem.PreparedAyahsCount : 1);
        }
        else if (SelectedSurah != null)
        {
            _onNavigateToStudio(SelectedSurah.Number, SelectedAyahNumber);
        }
    }

    [RelayCommand]
    private void OpenSearchResultInStudio(AyahSearchEntry entry)
    {
        _onNavigateToStudio(entry.SurahNumber, entry.AyahNumber);
    }
}

public sealed partial class SurahItemViewModel : ObservableObject
{
    public SurahInfo Info { get; }
    public int Number => Info.Number;
    public string NameArabic => Info.NameArabic;
    public string NameLatin => Info.NameLatin;
    public int AyahCount => Info.AyahCount;
    public string DisplayName => Info.DisplayName;
    public int StartPage => Info.StartPage;
    public int EndPage => Info.EndPage;
    public string SubtitleText => $"{AyahCount} آية • صفحة {StartPage} • {NameLatin}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(HasWorkStarted))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private int _preparedAyahsCount;

    public double ProgressPercentage => AyahCount > 0 ? (double)PreparedAyahsCount / AyahCount * 100.0 : 0.0;
    public string ProgressText => $"{PreparedAyahsCount} / {AyahCount}";

    [ObservableProperty]
    private bool _isAudioDownloaded;

    [ObservableProperty]
    private string _audioStatusText = "غير متوفر";

    [ObservableProperty]
    private string _audioDurationText = "—";

    public bool HasWorkStarted => PreparedAyahsCount > 0;
    public string ActionButtonText => HasWorkStarted ? "متابعة التقطيع" : "فتح السورة";

    public SurahItemViewModel(
        SurahInfo info,
        int preparedCount = 0,
        string duration = "—",
        bool isAudioDownloaded = false,
        string audioStatusText = "غير متوفر")
    {
        Info = info;
        PreparedAyahsCount = preparedCount;
        AudioDurationText = duration;
        IsAudioDownloaded = isAudioDownloaded;
        AudioStatusText = audioStatusText;
    }
}
