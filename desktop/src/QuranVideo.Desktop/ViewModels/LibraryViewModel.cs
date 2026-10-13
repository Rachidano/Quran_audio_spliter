using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly Action<int, int> _onNavigateToStudio;
    private readonly QuranSearchService _searchService;

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

    public ObservableCollection<SurahInfo> FilteredSurahs { get; } = new();
    public ObservableCollection<AyahSearchEntry> SearchResults { get; } = new();

    public LibraryViewModel(Action<int, int> onNavigateToStudio, QuranSearchService? searchService = null)
    {
        _onNavigateToStudio = onNavigateToStudio;
        _searchService = searchService ?? new QuranSearchService();
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
                SurahItems.Add(new SurahItemViewModel(s, GetDefaultPreparedCount(s.Number), GetDefaultDuration(s.Number)));
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
            SurahItems.Add(new SurahItemViewModel(s, GetDefaultPreparedCount(s.Number), GetDefaultDuration(s.Number)));
        }
        if (SelectedSurah == null || !FilteredSurahs.Contains(SelectedSurah))
        {
            SelectedSurah = FilteredSurahs.FirstOrDefault();
            SelectedSurahItem = SurahItems.FirstOrDefault();
        }
    }

    private static int GetDefaultPreparedCount(int surahNumber) => surahNumber switch
    {
        1 => 7,
        2 => 12,
        _ => 0
    };

    private static string GetDefaultDuration(int surahNumber) => surahNumber switch
    {
        1 => "00:00:49",
        2 => "01:28:16",
        _ => "—"
    };

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

    [ObservableProperty]
    private bool _hasLastWorkedAyah = true;

    [ObservableProperty]
    private int _lastWorkedSurahNumber = 2;

    [ObservableProperty]
    private int _lastWorkedAyahNumber = 13;

    [ObservableProperty]
    private string _lastWorkedDescription = "البقرة — الآية 13";

    [ObservableProperty]
    private string _lastWorkedAudioTime = "00:00 / 00:35";

    [ObservableProperty]
    private string _currentReciterName = "الشيخ صابر عبد الحكم";

    [ObservableProperty]
    private SurahItemViewModel? _selectedSurahItem;

    public ObservableCollection<SurahItemViewModel> SurahItems { get; } = new();

    public void SetLastWorkedAyah(int surahNumber, int ayahNumber)
    {
        if (surahNumber <= 0 || ayahNumber <= 0) return;
        var surah = SurahCatalogService.GetSurah(surahNumber);
        if (surah == null) return;

        LastWorkedSurahNumber = surahNumber;
        LastWorkedAyahNumber = ayahNumber;
        LastWorkedDescription = $"{surah.NameArabic} — الآية {ayahNumber}";
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(HasWorkStarted))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private int _preparedAyahsCount;

    public double ProgressPercentage => AyahCount > 0 ? (double)PreparedAyahsCount / AyahCount * 100.0 : 0.0;
    public string ProgressText => $"{PreparedAyahsCount} / {AyahCount}";

    [ObservableProperty]
    private bool _isAudioDownloaded = true;

    [ObservableProperty]
    private string _audioStatusText = "تم التنزيل";

    [ObservableProperty]
    private string _audioDurationText = "—";

    public bool HasWorkStarted => PreparedAyahsCount > 0;
    public string ActionButtonText => HasWorkStarted ? "متابعة التقطيع" : "فتح";

    public SurahItemViewModel(SurahInfo info, int preparedCount = 0, string duration = "—")
    {
        Info = info;
        PreparedAyahsCount = preparedCount;
        AudioDurationText = duration;
    }
}
