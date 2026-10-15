using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class StudioViewModel : ViewModelBase, IDisposable
{
    private readonly WorkspaceService _workspaceService;
    private readonly AnnotationService _annotationService;
    private readonly WaveformService _waveformService;
    private readonly AyahImageService _imageService;
    private readonly CompositionService _compositionService;

    public AudioPlayerService Player { get; }

    [ObservableProperty]
    private SurahInfo _currentSurah;

    [ObservableProperty]
    private int _currentAyahNumber = 1;

    [ObservableProperty]
    private bool _isAdvancedDrawerOpen;

    [ObservableProperty]
    private string _statusMessage = "جاهز للإعداد والتوليد";

    public string TargetDescription => $"سورة {CurrentSurah.NameArabic} — الآية {CurrentAyahNumber}";

    public StudioViewModel(
        WorkspaceService workspaceService,
        AnnotationService? annotationService = null,
        WaveformService? waveformService = null,
        AyahImageService? imageService = null,
        CompositionService? compositionService = null,
        int surahNumber = 1,
        int ayahNumber = 1)
    {
        _workspaceService = workspaceService;
        _annotationService = annotationService ?? new AnnotationService(workspaceService);
        _waveformService = waveformService ?? new WaveformService(workspaceService);
        _imageService = imageService ?? new AyahImageService(workspaceService);
        _compositionService = compositionService ?? new CompositionService(workspaceService, _imageService);


        Player = new AudioPlayerService();
        Player.PositionChanged += OnPlayerPositionChanged;
        Player.PlaybackStopped += OnPlayerPlaybackStopped;

        _currentSurah = SurahCatalogService.GetSurah(surahNumber) ?? SurahCatalogService.GetAllSurahs()[0];
        _currentAyahNumber = Math.Clamp(ayahNumber, 1, _currentSurah.AyahCount);
        _retakeTargetAyahNumber = _currentAyahNumber;

        UpdateCurrentPageNumber();
        // Sources are loaded after the workspace/catalog initialization.

    }

    public void SetTarget(int surahNumber, int ayahNumber)
    {
        var surah = SurahCatalogService.GetSurah(surahNumber);
        if (surah != null)
        {
            CurrentSurah = surah;
            CurrentAyahNumber = Math.Clamp(ayahNumber, 1, surah.AyahCount);
            RetakeTargetAyahNumber = CurrentAyahNumber; // L06-06: manual sync rule
            UpdateCurrentPageNumber();
            OnPropertyChanged(nameof(TargetDescription));
            OnPropertyChanged(nameof(RetakeTargetDescription));
            _ = LoadAudioAndPassagesForSurahAsync(surahNumber);
            _ = LoadAyahImageAsync();
        }
    }

    partial void OnCurrentAyahNumberChanged(int value)
    {
        UpdateCurrentPageNumber();
        OnPropertyChanged(nameof(TargetDescription));
        LoadPassageBoundsForCurrentAyah();
        _ = LoadAyahImageAsync();
    }

    /// <summary>Numéros d'ayah de la sourate courante (sélecteur de l'en-tête du studio).</summary>
    public IReadOnlyList<int> AyahNumbers => Enumerable.Range(1, CurrentSurah.AyahCount).ToList();

    /// <summary>Sélecteur de sourate du studio : changer de sourate ouvre son ayah 1.</summary>
    public SurahInfo StudioSurahSelection
    {
        get => CurrentSurah;
        set
        {
            if (value != null && value.Number != CurrentSurah.Number) SetTarget(value.Number, 1);
        }
    }

    partial void OnCurrentSurahChanged(SurahInfo value)
    {
        OnPropertyChanged(nameof(AyahNumbers));
        OnPropertyChanged(nameof(StudioSurahSelection));
        CurrentAyahNumber = Math.Clamp(CurrentAyahNumber, 1, value.AyahCount);
        RetakeTargetAyahNumber = CurrentAyahNumber;
        UpdateCurrentPageNumber();
        OnPropertyChanged(nameof(TargetDescription));
        OnPropertyChanged(nameof(RetakeTargetDescription));
        _ = LoadAudioAndPassagesForSurahAsync(value.Number);
        _ = LoadAyahImageAsync();
    }

    [RelayCommand]
    public void ToggleAdvancedDrawer()
    {
        IsAdvancedDrawerOpen = !IsAdvancedDrawerOpen;
    }

    // Manual navigation preserves position and start time (L06-04)
    [RelayCommand]
    public void NextAyah()
    {
        if (CurrentAyahNumber < CurrentSurah.AyahCount)
        {
            CurrentAyahNumber++;
            RetakeTargetAyahNumber = CurrentAyahNumber;
        }
        else if (CurrentSurah.Number < 114)
        {
            var nextSurah = SurahCatalogService.GetSurah(CurrentSurah.Number + 1);
            if (nextSurah != null)
            {
                CurrentSurah = nextSurah;
                CurrentAyahNumber = 1;
                RetakeTargetAyahNumber = 1;
            }
        }
    }

    [RelayCommand]
    public void PreviousAyah()
    {
        if (CurrentAyahNumber > 1)
        {
            CurrentAyahNumber--;
            RetakeTargetAyahNumber = CurrentAyahNumber;
        }
        else if (CurrentSurah.Number > 1)
        {
            var prevSurah = SurahCatalogService.GetSurah(CurrentSurah.Number - 1);
            if (prevSurah != null)
            {
                CurrentSurah = prevSurah;
                CurrentAyahNumber = prevSurah.AyahCount;
                RetakeTargetAyahNumber = prevSurah.AyahCount;
            }
        }
    }

    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        return AyahPassageEntity.FormatClock(seconds);
    }

    public static string FormatPassageKind(PassageKind kind) => kind switch
    {
        PassageKind.Full => "آية كاملة",
        PassageKind.Portion => "مقطع جزئي",
        PassageKind.Continuation => "متابعة / وصل",
        PassageKind.FullRetake => "إعادة كاملة",
        PassageKind.PartialRetake => "إعادة جزئية",
        _ => "غير محدد"
    };

    public void Dispose()
    {
        Player.PositionChanged -= OnPlayerPositionChanged;
        Player.PlaybackStopped -= OnPlayerPlaybackStopped;
        Player.Dispose();
        _videoCts?.Dispose();
    }
}
