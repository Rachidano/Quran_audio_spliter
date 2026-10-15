using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

/// <summary>
/// Studio — annotation audio : chargement de la récitation, choix du récitateur, lecture,
/// bornes de l'ayah (F7/F8), reprises, liste des segments, annulation et import/export JSON.
/// </summary>
public sealed partial class StudioViewModel
{
    private bool _isOperationBusy;
    private string? _currentAudioPath;

    [ObservableProperty]
    private int _retakeTargetAyahNumber = 1;

    [ObservableProperty]
    private PassageKind _selectedPassageKind = PassageKind.Full;

    [ObservableProperty]
    private double _startTimeSec = 0.0;

    [ObservableProperty]
    private double _endTimeSec = 6.0;

    [ObservableProperty]
    private double _currentPlaybackTimeSec = 0.0;

    [ObservableProperty]
    private double _audioDurationSec = 0.0;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private float[] _waveformPeaks = Array.Empty<float>();

    [ObservableProperty]
    private bool _isCurrentPassageValidated;

    [ObservableProperty]
    private string _currentReciterId = "saber_abdelhakam";

    [ObservableProperty]
    private string _currentReciterNameArabic = "الشيخ صابر عبد الحكم";

    [ObservableProperty]
    private string _currentCollectionId = "saber_abdelhakam_warsh";

    [ObservableProperty]
    private string _currentAudioSourceId = "saber_abdelhakam_warsh:001";

    [ObservableProperty]
    private string _currentAudioSha256 = string.Empty;

    public ObservableCollection<AyahPassageEntity> RecordedPassages { get; } = new();
    public string RetakeTargetDescription => $"سورة {CurrentSurah.NameArabic} — الآية {RetakeTargetAyahNumber}";

    public string FormattedStartTime => FormatTime(StartTimeSec);
    public string FormattedEndTime => FormatTime(EndTimeSec);
    public string FormattedPlaybackTime => FormatTime(CurrentPlaybackTimeSec);
    public string FormattedTotalDuration => FormatTime(AudioDurationSec);

    public string RecordedPassagesCountDescription => $"{RecordedPassages.Select(p => p.AyahNumber).Distinct().Count()} / {CurrentSurah.AyahCount} آية مسجلة";
    public bool HasRecordedPassages => RecordedPassages.Count > 0;
    public bool HasNoRecordedPassages => RecordedPassages.Count == 0;
    public bool HasCurrentPassage => RecordedPassages.Any(p => p.AyahNumber == CurrentAyahNumber);

    partial void OnRetakeTargetAyahNumberChanged(int value)
    {
        OnPropertyChanged(nameof(RetakeTargetDescription));
    }

    partial void OnStartTimeSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedStartTime));
    }

    partial void OnEndTimeSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedEndTime));
    }

    partial void OnCurrentPlaybackTimeSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedPlaybackTime));
    }

    partial void OnAudioDurationSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedTotalDuration));
    }

    private int _audioLoadVersion;
    public ObservableCollection<RecitationChoice> Reciters { get; } = new();
    [ObservableProperty] private RecitationChoice? _selectedRecitation;
    [ObservableProperty] private bool _isAudioDownloading;
    private CancellationTokenSource? _audioDownload;

    partial void OnSelectedRecitationChanged(RecitationChoice? value)
    {
        if (value == null) return;
        CurrentReciterId = value.ReciterId;
        CurrentCollectionId = value.CollectionId;
        CurrentReciterNameArabic = value.Name;
        _ = RefreshReadyTimingsAvailabilityAsync();
        _ = LoadAudioAndPassagesForSurahAsync(CurrentSurah.Number);
    }

    public async Task RefreshRecitersAsync(bool selectDefault = false)
    {
        var catalog = new RecitationCatalogService(_workspaceService);
        await catalog.InitializeAsync();
        var id = selectDefault ? await catalog.GetDefaultAsync() : CurrentCollectionId;
        Reciters.Clear();
        foreach (var choice in await catalog.GetCollectionsAsync()) Reciters.Add(choice);
        SelectedRecitation = Reciters.FirstOrDefault(r => r.CollectionId == id) ?? Reciters.First();
    }

    [RelayCommand]
    private async Task ConfigureAudioSourcesAsync()
    {
        Player.Pause();
        var window = new QuranVideo.Desktop.Views.AudioSourcesWindow(_workspaceService)
        { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
        await RefreshRecitersAsync(selectDefault: true);
    }

    public async Task LoadAudioAndPassagesForSurahAsync(int surahNumber)
    {
        var version = ++_audioLoadVersion;
        var collectionId = CurrentCollectionId;
        CurrentAudioSourceId = $"{collectionId}:{surahNumber:D3}";
        Player.Dispose();
        IsPlaying = false; AudioDurationSec = 0; CurrentPlaybackTimeSec = 0;
        WaveformPeaks = Array.Empty<float>(); RecordedPassages.Clear(); CurrentAudioSha256 = string.Empty;
        try
        {
            var source = await new RecitationLibraryService(_workspaceService).GetAudioSourceAsync(collectionId, surahNumber);
            if (version != _audioLoadVersion) return;
            var path = source == null ? null : _workspaceService.ToAbsolutePath(source.RelativeFilePath);
            if (path != null && File.Exists(path))
            {
                _currentAudioPath = path;
                Player.Load(path);
                CurrentAudioSha256 = source!.Sha256;
                AudioDurationSec = Player.DurationSec;
                var peaks = await _waveformService.GetWaveformPeaksAsync(path, source!.Sha256);
                if (version != _audioLoadVersion) return;
                WaveformPeaks = peaks;
                StatusMessage = $"تم تحميل تلاوة {CurrentReciterNameArabic} ({FormattedTotalDuration})";
            }
            else
            {
                _currentAudioPath = null;
                StatusMessage = "التلاوة غير منزلة لهذا القارئ. اضغط تنزيل أو افتح إعداد روابط السور.";
            }
            await RefreshPassagesListAsync();
            if (version == _audioLoadVersion) LoadPassageBoundsForCurrentAyah();
        }
        catch (Exception ex)
        {
            if (version == _audioLoadVersion) StatusMessage = $"تعذر فتح التلاوة: {ex.Message}";
        }
    }

    private async Task RefreshPassagesListAsync()
    {
        var sourceId = CurrentAudioSourceId;
        var surahNumber = CurrentSurah.Number;
        var passages = await _annotationService.GetPassagesForSurahAsync(sourceId, surahNumber);
        if (sourceId != CurrentAudioSourceId || surahNumber != CurrentSurah.Number) return;
        RecordedPassages.Clear();
        foreach (var p in passages.OrderBy(p => p.AyahNumber).ThenBy(p => p.Revision))
        {
            RecordedPassages.Add(p);
        }
        OnPropertyChanged(nameof(RecordedPassagesCountDescription));
        OnPropertyChanged(nameof(ProposedPassagesCount));
        OnPropertyChanged(nameof(HasRecordedPassages));
        OnPropertyChanged(nameof(HasNoRecordedPassages));
        OnPropertyChanged(nameof(HasCurrentPassage));
    }

    private void LoadPassageBoundsForCurrentAyah()
    {
        var existing = RecordedPassages
            .Where(p => p.AyahNumber == CurrentAyahNumber && p.Kind == PassageKind.Full)
            .OrderByDescending(p => p.Revision)
            .FirstOrDefault();

        if (existing != null)
        {
            StartTimeSec = existing.StartTimeSec;
            EndTimeSec = existing.EndTimeSec;
            IsCurrentPassageValidated = existing.IsValidatedByUser;
        }
        else
        {
            var prev = RecordedPassages
                .Where(p => p.AyahNumber == CurrentAyahNumber - 1 && p.Kind == PassageKind.Full)
                .OrderByDescending(p => p.Revision)
                .FirstOrDefault();
            if (prev != null)
            {
                StartTimeSec = prev.EndTimeSec;
                EndTimeSec = Math.Round(prev.EndTimeSec + 5.0, 2);
            }
            else
            {
                StartTimeSec = 0.0;
                EndTimeSec = 5.0;
            }
            IsCurrentPassageValidated = false;
        }
        OnPropertyChanged(nameof(HasCurrentPassage));
    }

    private void OnPlayerPositionChanged(double currentSec)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() =>
            {
                CurrentPlaybackTimeSec = Math.Round(currentSec, 2);
                IsPlaying = Player.IsPlaying;
            });
        }
        else
        {
            CurrentPlaybackTimeSec = Math.Round(currentSec, 2);
            IsPlaying = Player.IsPlaying;
        }
    }

    private void OnPlayerPlaybackStopped()
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => IsPlaying = false);
        }
        else
        {
            IsPlaying = false;
        }
    }

    [RelayCommand]
    public async Task ImportLocalAudioAsync()
    {
        var openDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"اختر الملف الصوتي لسورة {CurrentSurah.NameArabic}",
            Filter = "ملفات صوتية (*.mp3;*.wav)|*.mp3;*.wav|كل الملفات (*.*)|*.*"
        };

        if (openDialog.ShowDialog() == true)
        {
            StatusMessage = "جارٍ استيراد الملف الصوتي وحساب مخطط الصوت...";
            var reciterService = new RecitationLibraryService(_workspaceService);
            var (ok, src, err) = await reciterService.ImportLocalAudioAsync(CurrentCollectionId, CurrentSurah.Number, openDialog.FileName);
            if (ok)
            {
                await LoadAudioAndPassagesForSurahAsync(CurrentSurah.Number);
                StatusMessage = $"تم استيراد الملف الصوتي بنجاح: {Path.GetFileName(openDialog.FileName)}";
            }
            else
            {
                StatusMessage = $"فشل استيراد الملف الصوتي: {err}";
            }
        }
    }

    [RelayCommand]
    public async Task DownloadSurahAudioAsync()
    {
        if (IsAudioDownloading) return;
        var collection = CurrentCollectionId;
        var surah = CurrentSurah.Number;
        IsAudioDownloading = true;
        _audioDownload = new CancellationTokenSource();
        try
        {
            StatusMessage = $"جارٍ تنزيل تلاوة سورة {CurrentSurah.NameArabic}...";
            var progress = new Progress<double>(p => StatusMessage = p < 0 ? "جارٍ تنزيل التلاوة..." : $"جارٍ تنزيل التلاوة: {p:F0}%");
            var result = await new RecitationLibraryService(_workspaceService).DownloadSurahAudioAsync(collection, surah, progress, _audioDownload.Token);
            if (result.Success && collection == CurrentCollectionId && surah == CurrentSurah.Number)
                await LoadAudioAndPassagesForSurahAsync(surah);
            StatusMessage = result.Success ? "تم تنزيل التلاوة وحفظها بنجاح." : result.ErrorMessage ?? "تعذر التنزيل.";
        }
        finally { _audioDownload.Dispose(); _audioDownload = null; IsAudioDownloading = false; }
    }

    [RelayCommand] private void CancelAudioDownload() => _audioDownload?.Cancel();

    [RelayCommand]
    public async Task SelectReciterAsync(string reciterId)
    {
        await RefreshRecitersAsync();
        SelectedRecitation = Reciters.FirstOrDefault(r => r.ReciterId == reciterId) ?? SelectedRecitation;
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (Player.IsPlaying)
        {
            Player.Pause();
            IsPlaying = false;
        }
        else
        {
            Player.Play();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    public void Seek(double timeSec)
    {
        var max = AudioDurationSec > 0 ? AudioDurationSec : double.MaxValue;
        var target = Math.Max(0, Math.Round(timeSec, 2));
        if (target > max) target = max;
        CurrentPlaybackTimeSec = target;
        Player.Seek(target);
    }

    [RelayCommand]
    public void StepForward()
    {
        var max = AudioDurationSec > 0 ? AudioDurationSec : double.MaxValue;
        var target = Math.Round(CurrentPlaybackTimeSec + 1.0, 2);
        if (target > max) target = max;
        Seek(target);
    }

    [RelayCommand]
    public void StepBackward()
    {
        var target = Math.Max(0.0, Math.Round(CurrentPlaybackTimeSec - 1.0, 2));
        Seek(target);
    }

    // L06-02: F7 - Set start time to cursor, formatted in minutes:seconds
    [RelayCommand]
    public void SetStartFromCursor()
    {
        StartTimeSec = Math.Round(CurrentPlaybackTimeSec, 2);
        if (EndTimeSec <= StartTimeSec)
        {
            EndTimeSec = Math.Round(StartTimeSec + 5.0, 2);
        }
        StatusMessage = $"تم تحديد بداية الآية عند: {FormattedStartTime}";
    }

    // L06-03: F8 / Enter - Record end time and advance to next ayah
    [RelayCommand]
    public async Task SaveEndAndAdvanceAsync()
    {
        if (_isOperationBusy) return;
        _isOperationBusy = true;

        try
        {
            EndTimeSec = Math.Round(CurrentPlaybackTimeSec, 2);

            var passage = new AyahPassageEntity
            {
                AudioSourceId = CurrentAudioSourceId,
                SurahNumber = CurrentSurah.Number,
                AyahNumber = CurrentAyahNumber,
                AyahId = $"{CurrentSurah.Number}:{CurrentAyahNumber}",
                StartTimeSec = StartTimeSec,
                EndTimeSec = EndTimeSec,
                Kind = PassageKind.Full,
                IsSelectedForExport = true,
                IsValidatedByUser = true
            };

            var (success, error, saved) = await _annotationService.SavePassageAsync(passage, AudioDurationSec);
            if (!success)
            {
                StatusMessage = $"خطأ: {error}";
                return;
            }

            // L06-06: Retake target explicitly set to the just finished ayah!
            RetakeTargetAyahNumber = CurrentAyahNumber;

            await RefreshPassagesListAsync();

            // Next start time = current recorded end time
            var nextStart = EndTimeSec;

            // Check if last ayah of surah
            if (CurrentAyahNumber >= CurrentSurah.AyahCount)
            {
                StatusMessage = $"تم تسجيل الآية {CurrentAyahNumber}. اكتملت جميع آيات سورة {CurrentSurah.NameArabic} بنجاح!";
            }
            else
            {
                CurrentAyahNumber++;
                StartTimeSec = nextStart;
                EndTimeSec = Math.Round(nextStart + 5.0, 2);
                StatusMessage = $"تم حفظ الآية والانتقال للآية {CurrentAyahNumber} (البداية: {FormattedStartTime})";
            }
        }
        finally
        {
            _isOperationBusy = false;
        }
    }

    // L06-05 & L06-06: F9 - Save Retake (المقطع المُعاد)
    [RelayCommand]
    public async Task SaveRetakeAsync()
    {
        if (_isOperationBusy) return;
        _isOperationBusy = true;

        try
        {
            var end = Math.Round(CurrentPlaybackTimeSec, 2);
            if (end <= StartTimeSec)
            {
                end = Math.Round(StartTimeSec + 5.0, 2);
            }

            var passage = new AyahPassageEntity
            {
                AudioSourceId = CurrentAudioSourceId,
                SurahNumber = CurrentSurah.Number,
                AyahNumber = RetakeTargetAyahNumber,
                AyahId = $"{CurrentSurah.Number}:{RetakeTargetAyahNumber}",
                StartTimeSec = StartTimeSec,
                EndTimeSec = end,
                Kind = SelectedPassageKind == PassageKind.Full ? PassageKind.FullRetake : SelectedPassageKind,
                IsSelectedForExport = true,
                IsValidatedByUser = true
            };

            var (success, error, saved) = await _annotationService.SavePassageAsync(passage, AudioDurationSec);
            if (!success)
            {
                StatusMessage = $"خطأ في حفظ الإعادة: {error}";
                return;
            }

            await RefreshPassagesListAsync();
            StatusMessage = $"تم حفظ المقطع المُعاد بنجاح للهدف: {RetakeTargetDescription} ({FormatPassageKind(passage.Kind)})";
        }
        finally
        {
            _isOperationBusy = false;
        }
    }

    // L06-07: Preview segment (معاينة المقطع)
    [RelayCommand]
    public void PreviewSegment(bool loop = false)
    {
        Player.PlaySegment(StartTimeSec, EndTimeSec, loop);
        IsPlaying = true;
        StatusMessage = $"معاينة المقطع: من {FormattedStartTime} إلى {FormattedEndTime}";
    }

    [RelayCommand]
    public void SelectPassage(AyahPassageEntity? passage)
    {
        if (passage == null) return;
        CurrentAyahNumber = passage.AyahNumber;
        RetakeTargetAyahNumber = passage.AyahNumber;
        StartTimeSec = passage.StartTimeSec;
        EndTimeSec = passage.EndTimeSec;
        IsCurrentPassageValidated = passage.IsValidatedByUser;
        Seek(passage.StartTimeSec);
        StatusMessage = $"تم تحديد الآية {passage.AyahNumber} ({FormatTime(passage.StartTimeSec)} ➔ {FormatTime(passage.EndTimeSec)})";
    }

    [RelayCommand]
    public void PreviewPassage(AyahPassageEntity? passage)
    {
        if (passage == null) return;
        SelectPassage(passage);
        PreviewSegment();
    }

    [RelayCommand]
    public async Task SaveCurrentPassageAsync()
    {
        if (_isOperationBusy) return;
        _isOperationBusy = true;

        try
        {
            var passage = new AyahPassageEntity
            {
                AudioSourceId = CurrentAudioSourceId,
                SurahNumber = CurrentSurah.Number,
                AyahNumber = CurrentAyahNumber,
                AyahId = $"{CurrentSurah.Number}:{CurrentAyahNumber}",
                StartTimeSec = StartTimeSec,
                EndTimeSec = EndTimeSec,
                Kind = PassageKind.Full,
                IsSelectedForExport = true,
                IsValidatedByUser = true
            };

            var (success, error, saved) = await _annotationService.SavePassageAsync(passage, AudioDurationSec);
            if (!success)
            {
                StatusMessage = $"خطأ: {error}";
                return;
            }

            RetakeTargetAyahNumber = CurrentAyahNumber;
            await RefreshPassagesListAsync();
            StatusMessage = $"تم حفظ تعديل مقطع الآية {CurrentAyahNumber} بنجاح ({FormattedStartTime} ➔ {FormattedEndTime}) 💾";
        }
        finally
        {
            _isOperationBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeletePassageAsync(AyahPassageEntity? passage)
    {
        if (passage == null) return;

        var ayahNum = passage.AyahNumber;
        var ok = await _annotationService.DeletePassageAsync(passage.Id);
        if (ok)
        {
            await RefreshPassagesListAsync();
            if (CurrentAyahNumber == ayahNum)
            {
                LoadPassageBoundsForCurrentAyah();
            }
            StatusMessage = $"تم حذف مقطع الآية {ayahNum} بنجاح 🗑️";
        }
        else
        {
            StatusMessage = $"تعذر حذف المقطع للآية {ayahNum}.";
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentPassageAsync()
    {
        var existing = RecordedPassages
            .Where(p => p.AyahNumber == CurrentAyahNumber && p.Kind == PassageKind.Full)
            .OrderByDescending(p => p.Revision)
            .FirstOrDefault();

        if (existing != null)
        {
            await DeletePassageAsync(existing);
        }
        else
        {
            ResetCurrentAyahTimings();
        }
    }

    [RelayCommand]
    public void ResetCurrentAyahTimings()
    {
        StartTimeSec = 0.0;
        EndTimeSec = 5.0;
        IsCurrentPassageValidated = false;
        StatusMessage = $"تمت إعادة ضبط التوقيتات للآية {CurrentAyahNumber}.";
    }

    // L06-07: Undo last save (Ctrl+Z)
    [RelayCommand]
    public async Task UndoLastSaveAsync()
    {
        if (!_annotationService.CanUndo)
        {
            StatusMessage = "لا توجد عمليات سابقة للتراجع عنها.";
            return;
        }

        var undone = await _annotationService.UndoLastSaveAsync();
        if (undone != null)
        {
            await RefreshPassagesListAsync();
            StatusMessage = $"تم التراجع عن تسجيل الآية {undone.AyahNumber} ({FormattedStartTime})";
        }
    }

    // L06-02: Fine micro-adjustments
    [RelayCommand]
    public void AdjustStart(object? parameter)
    {
        var delta = ParseDouble(parameter);
        StartTimeSec = Math.Max(0, Math.Round(StartTimeSec + delta, 2));
    }

    [RelayCommand]
    public void AdjustEnd(object? parameter)
    {
        var delta = ParseDouble(parameter);
        EndTimeSec = Math.Max(StartTimeSec + 0.1, Math.Round(EndTimeSec + delta, 2));
    }

    private static double ParseDouble(object? val)
    {
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val != null && double.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return 0.0;
    }

    // L06-09: Export annotations
    [RelayCommand]
    public async Task ExportAnnotationsAsync()
    {
        try
        {
            var exportDir = Path.Combine(_workspaceService.WorkspacePath, "annotations");
            Directory.CreateDirectory(exportDir);
            var exportPath = Path.Combine(exportDir, $"annotations_{CurrentSurah.Number:D3}.json");

            await _annotationService.ExportAnnotationsToJsonAsync(CurrentAudioSourceId, CurrentSurah.Number, exportPath);
            StatusMessage = $"تم تصدير التوقيتات بنجاح إلى: {Path.GetFileName(exportPath)}";
            LoggingService.Instance.LogInfo($"Annotations exportées : {exportPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"فشل تصدير التوقيتات: {ex.Message}";
        }
    }

    // L06-09: Import annotations
    [RelayCommand]
    public async Task ImportAnnotationsAsync(string jsonFilePath)
    {
        try
        {
            var (success, error, count) = await _annotationService.ImportAnnotationsFromJsonAsync(
                jsonFilePath,
                CurrentAudioSha256,
                CurrentAudioSourceId,
                CurrentSurah.Number);

            if (success)
            {
                await RefreshPassagesListAsync();
                LoadPassageBoundsForCurrentAyah();
                StatusMessage = $"تم استيراد {count} مقطع بنجاح.";
            }
            else
            {
                StatusMessage = $"فشل الاستيراد: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في استيراد التوقيتات: {ex.Message}";
        }
    }
}
