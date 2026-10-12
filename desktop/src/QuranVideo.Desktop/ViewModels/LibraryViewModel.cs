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
        foreach (var s in all)
        {
            if (s.Number.ToString().Contains(query) ||
                s.NameArabic.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.NameLatin.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSurahs.Add(s);
            }
        }

        if (SelectedSurah == null || !FilteredSurahs.Contains(SelectedSurah))
        {
            SelectedSurah = FilteredSurahs.FirstOrDefault();
        }
    }

    private void LoadAllSurahs()
    {
        FilteredSurahs.Clear();
        foreach (var s in SurahCatalogService.GetAllSurahs())
        {
            FilteredSurahs.Add(s);
        }
        if (SelectedSurah == null || !FilteredSurahs.Contains(SelectedSurah))
        {
            SelectedSurah = FilteredSurahs.FirstOrDefault();
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

    [RelayCommand]
    private void OpenInStudio()
    {
        if (IsSearchMode && SelectedSearchResult != null)
        {
            _onNavigateToStudio(SelectedSearchResult.SurahNumber, SelectedSearchResult.AyahNumber);
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
