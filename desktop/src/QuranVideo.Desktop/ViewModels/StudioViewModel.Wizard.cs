using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed class WizardAyahItemViewModel : ObservableObject
{
    public Action? OnStatusChanged { get; set; }

    public int AyahNumber { get; set; }
    public string AyahId { get; set; } = string.Empty;
    public string Description => $"الآية {AyahNumber}";

    public List<AyahPassageEntity> AvailableTakes { get; set; } = new();

    private AyahPassageEntity? _selectedTake;
    public AyahPassageEntity? SelectedTake
    {
        get => _selectedTake;
        set
        {
            if (SetProperty(ref _selectedTake, value))
            {
                OnPropertyChanged(nameof(IsAudioAnnotated));
                OnPropertyChanged(nameof(IsFullyReady));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusColor));
                OnStatusChanged?.Invoke();
            }
        }
    }

    public int ImageRecipeId { get; set; }
    public int ImageRevision { get; set; }
    public string? ImagePath { get; set; }

    // Gap / Junction review tracking
    private bool _hasGapWithPrevious;
    public bool HasGapWithPrevious
    {
        get => _hasGapWithPrevious;
        set
        {
            if (SetProperty(ref _hasGapWithPrevious, value))
            {
                OnPropertyChanged(nameof(GapDescription));
                OnPropertyChanged(nameof(IsFullyReady));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    private double _gapSeconds;
    public double GapSeconds
    {
        get => _gapSeconds;
        set
        {
            if (SetProperty(ref _gapSeconds, value))
            {
                OnPropertyChanged(nameof(GapDescription));
            }
        }
    }

    public string GapDescription => $"فاصل زمني ({GapSeconds:F1} ث) يتطلب التأكيد";

    private bool _junctionReviewed;
    public bool JunctionReviewed
    {
        get => _junctionReviewed;
        set
        {
            if (SetProperty(ref _junctionReviewed, value))
            {
                OnPropertyChanged(nameof(IsFullyReady));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusColor));
                OnStatusChanged?.Invoke();
            }
        }
    }

    // Saved snapshot state & unconfirmed modification tracking (R01, R02)
    public int SavedPassageId { get; set; }
    public int SavedPassageRevision { get; set; }
    public double SavedStartTimeSec { get; set; }
    public double SavedEndTimeSec { get; set; }
    public int SavedImageRecipeId { get; set; }
    public int SavedImageRevision { get; set; }
    public bool HasMissingSavedTake { get; set; }

    private bool _hasUnconfirmedModification;
    public bool HasUnconfirmedModification
    {
        get => _hasUnconfirmedModification;
        set
        {
            if (SetProperty(ref _hasUnconfirmedModification, value))
            {
                OnPropertyChanged(nameof(IsFullyReady));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    private bool _isModificationConfirmedByUser = true;
    public bool IsModificationConfirmedByUser
    {
        get => _isModificationConfirmedByUser;
        set
        {
            if (SetProperty(ref _isModificationConfirmedByUser, value))
            {
                OnPropertyChanged(nameof(IsFullyReady));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusColor));
                OnStatusChanged?.Invoke();
            }
        }
    }

    public void ConfirmModification()
    {
        if (SelectedTake != null)
        {
            SavedPassageRevision = SelectedTake.Revision;
            SavedStartTimeSec = SelectedTake.StartTimeSec;
            SavedEndTimeSec = SelectedTake.EndTimeSec;
        }
        SavedImageRevision = ImageRevision;
        SavedImageRecipeId = ImageRecipeId;
        HasUnconfirmedModification = false;
        IsModificationConfirmedByUser = true;
        OnStatusChanged?.Invoke();
    }

    public bool IsAudioAnnotated => SelectedTake != null &&
                                    (SelectedTake.Kind == PassageKind.Full || SelectedTake.Kind == PassageKind.FullRetake);

    public bool IsFullyReady => IsAudioAnnotated &&
                                !string.IsNullOrEmpty(ImagePath) &&
                                File.Exists(ImagePath) &&
                                (!HasGapWithPrevious || JunctionReviewed) &&
                                (!HasUnconfirmedModification || IsModificationConfirmedByUser);

    public string StatusBadge => IsFullyReady ? "✔ جاهز" :
                                 HasMissingSavedTake ? "❌ التسجيل المحفوظ مفقود" :
                                 HasUnconfirmedModification ? "⚠️ تعديل غير معتمد" :
                                 (HasGapWithPrevious && !JunctionReviewed) ? "⚠️ فاصل غير مؤكد" :
                                 "⚠ مراجعة مطلوبة";

    public string StatusColor => IsFullyReady ? "#15803d" : "#b91c1c";
}

public sealed partial class StudioViewModel
{
    [ObservableProperty]
    private bool _isWizardMode = false;

    public bool IsFineTuningMode
    {
        get => !IsWizardMode;
        set => IsWizardMode = !value;
    }

    partial void OnIsWizardModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFineTuningMode));
    }

    [ObservableProperty]
    private int _wizardStep = 1; // 1: اختيار الآيات, 2: اختيار المظهر, 3: معاينة, 4: تصدير وحفظ

    public bool IsStep1 => WizardStep == 1;
    public bool IsStep2 => WizardStep == 2;
    public bool IsStep3 => WizardStep == 3;
    public bool IsStep4 => WizardStep == 4;

    partial void OnWizardStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
    }

    [ObservableProperty]
    private int _wizardStartAyah = 1;

    [ObservableProperty]
    private int _wizardEndAyah = 1;

    [ObservableProperty]
    private string _projectTitle = string.Empty;

    [ObservableProperty]
    private VideoOrientation _wizardOrientation = VideoOrientation.Horizontal;

    [ObservableProperty]
    private BackgroundType _wizardBackground = BackgroundType.Light;

    [ObservableProperty]
    private string? _wizardCustomBackgroundPath;

    [ObservableProperty]
    private WatermarkType _wizardWatermark = WatermarkType.None;

    [ObservableProperty]
    private string? _wizardWatermarkImagePath;

    [ObservableProperty]
    private string _wizardWatermarkText = string.Empty;

    [ObservableProperty]
    private WatermarkPosition _wizardWatermarkPosition = WatermarkPosition.BottomLeft;

    [ObservableProperty]
    private double _wizardWatermarkOpacity = 0.85;

    [ObservableProperty]
    private double _wizardWatermarkScale = 1.0;

    partial void OnWizardWatermarkScaleChanged(double value)
    {
        UpdateWizardPreview();
    }

    partial void OnWizardWatermarkOpacityChanged(double value)
    {
        UpdateWizardPreview();
    }

    partial void OnWizardWatermarkTextChanged(string value)
    {
        UpdateWizardPreview();
    }

    [ObservableProperty]
    private BitmapSource? _wizardPreviewImage;

    [ObservableProperty]
    private int _previewAyahIndex = 0;

    [ObservableProperty]
    private bool _isWizardSequenceValid;

    [ObservableProperty]
    private string? _wizardValidationSummary;

    [ObservableProperty]
    private bool _isProjectExporting;

    [ObservableProperty]
    private int _projectExportProgress;

    [ObservableProperty]
    private string? _projectExportError;

    public bool HasProjectExportError => !string.IsNullOrEmpty(ProjectExportError);

    partial void OnProjectExportErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProjectExportError));
    }

    [ObservableProperty]
    private int? _activeProjectId;

    public ObservableCollection<WizardAyahItemViewModel> WizardAyatItems { get; } = new();
    public ObservableCollection<VideoProjectEntity> SavedProjects { get; } = new();

    [RelayCommand]
    public void ToggleStudioMode()
    {
        IsWizardMode = !IsWizardMode;
    }

    [RelayCommand]
    public void SelectWizardMode()
    {
        IsWizardMode = true;
    }

    [RelayCommand]
    public void SelectFineTuningMode()
    {
        IsWizardMode = false;
    }

    [RelayCommand]
    public async Task OpenWizardForCurrentAyahAsync()
    {
        WizardStartAyah = CurrentAyahNumber;
        WizardEndAyah = CurrentAyahNumber;
        IsWizardMode = true;
        await RefreshWizardSequenceAsync();
        WizardStep = 2; // Transition directly to Step 2 (Appearance & Watermark Customization)
        UpdateWizardPreview();
    }

    [RelayCommand]
    public void SetOrientation(string orientation)
    {
        WizardOrientation = orientation == "Vertical" ? VideoOrientation.Vertical : VideoOrientation.Horizontal;
        UpdateWizardPreview();
    }

    [RelayCommand]
    public void SetBackground(string bg)
    {
        WizardBackground = bg switch
        {
            "Dark" => BackgroundType.Dark,
            "Custom" => BackgroundType.CustomImage,
            _ => BackgroundType.Light
        };
        UpdateWizardPreview();
    }

    [RelayCommand]
    public void SetWatermarkType(string wm)
    {
        WizardWatermark = wm switch
        {
            "Logo" => WatermarkType.Logo,
            "Text" => WatermarkType.Text,
            _ => WatermarkType.None
        };
        UpdateWizardPreview();
    }

    [RelayCommand]
    public void SetWatermarkPosition(string pos)
    {
        WizardWatermarkPosition = pos switch
        {
            "TopRight" => WatermarkPosition.TopRight,
            "TopLeft" => WatermarkPosition.TopLeft,
            "BottomRight" => WatermarkPosition.BottomRight,
            "BottomLeft" or _ => WatermarkPosition.BottomLeft
        };
        UpdateWizardPreview();
    }

    [RelayCommand]
    public void GoToWizardStep(object? stepObj)
    {
        if (stepObj == null) return;
        int step = stepObj is int i ? i : int.TryParse(stepObj.ToString(), out var parsed) ? parsed : 1;
        if (step >= 1 && step <= 4)
        {
            WizardStep = step;
            if (WizardStep == 3)
            {
                UpdateWizardPreview();
            }
        }
    }

    [RelayCommand]
    public void NextWizardStep()
    {
        if (WizardStep < 4)
        {
            GoToWizardStep(WizardStep + 1);
        }
    }

    [RelayCommand]
    public void PreviousWizardStep()
    {
        if (WizardStep > 1)
        {
            GoToWizardStep(WizardStep - 1);
        }
    }

    [RelayCommand]
    public async Task RefreshWizardSequenceAsync()
    {
        if (CurrentSurah == null) return;

        int start = Math.Min(WizardStartAyah, WizardEndAyah);
        int end = Math.Max(WizardStartAyah, WizardEndAyah);

        if (start < 1) start = 1;
        if (end > CurrentSurah.AyahCount) end = CurrentSurah.AyahCount;

        WizardStartAyah = start;
        WizardEndAyah = end;

        WizardAyatItems.Clear();

        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        string audioSourceId = CurrentAudioSourceId;

        for (int ayahNum = start; ayahNum <= end; ayahNum++)
        {
            string ayahId = $"{CurrentSurah.Number}:{ayahNum}";

            // Find passages for this ayah
            var passages = await db.Passages
                .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId)
                .OrderByDescending(p => p.Revision)
                .ToListAsync();

            var fullTakes = passages
                .Where(p => p.Kind == PassageKind.Full || p.Kind == PassageKind.FullRetake)
                .ToList();

            // If only one full take exists, select it; if multiple full takes exist, require explicit user choice (R03)
            AyahPassageEntity? selectedTake = null;
            if (fullTakes.Count == 1)
            {
                selectedTake = fullTakes[0];
            }
            else
            {
                // When multiple full takes exist, require explicit selection (selectedTake remains null)
                selectedTake = null;
            }

            // Find image recipe
            var recipe = await db.ImageRecipes
                .FirstOrDefaultAsync(r => r.AyahId == ayahId);

            string? imagePath = null;
            if (recipe != null)
            {
                imagePath = _workspaceService.ToAbsolutePath(recipe.RelativeImagePath);
            }

            var item = new WizardAyahItemViewModel
            {
                AyahNumber = ayahNum,
                AyahId = ayahId,
                AvailableTakes = passages,
                SelectedTake = selectedTake,
                ImageRecipeId = recipe?.Id ?? 0,
                ImageRevision = recipe?.Revision ?? 0,
                ImagePath = imagePath,
                OnStatusChanged = RecalculateGapsAndStatus
            };

            WizardAyatItems.Add(item);
        }

        RecalculateGapsAndStatus();

        if (string.IsNullOrWhiteSpace(ProjectTitle))
        {
            ProjectTitle = $"سورة {CurrentSurah.NameArabic} ({start}-{end})";
        }
    }

    public void RecalculateGapsAndStatus()
    {
        for (int i = 0; i < WizardAyatItems.Count; i++)
        {
            var item = WizardAyatItems[i];
            if (i == 0)
            {
                item.HasGapWithPrevious = false;
                item.GapSeconds = 0;
            }
            else
            {
                var prev = WizardAyatItems[i - 1];
                if (prev.SelectedTake != null && item.SelectedTake != null)
                {
                    double gap = item.SelectedTake.StartTimeSec - prev.SelectedTake.EndTimeSec;
                    if (gap > 0.15)
                    {
                        item.HasGapWithPrevious = true;
                        item.GapSeconds = gap;
                    }
                    else
                    {
                        item.HasGapWithPrevious = false;
                        item.GapSeconds = 0;
                    }
                }
                else
                {
                    item.HasGapWithPrevious = false;
                    item.GapSeconds = 0;
                }
            }
        }

        UpdateWizardSequenceValidity();
    }

    private void UpdateWizardSequenceValidity()
    {
        IsWizardSequenceValid = WizardAyatItems.Count > 0 && WizardAyatItems.All(i => i.IsFullyReady);
        int start = WizardStartAyah;
        int end = WizardEndAyah;
        if (IsWizardSequenceValid)
        {
            WizardValidationSummary = $"جميع الآيات من {start} إلى {end} مسجلة وجاهزة للتصدير.";
        }
        else
        {
            var missing = WizardAyatItems.Where(i => !i.IsFullyReady).Select(i => i.AyahNumber);
            WizardValidationSummary = $"تنبيه: الآيات التالية تتطلب تسجيلاً صوتياً أو صورة أو اختيار تسجيل: {string.Join("، ", missing)}";
        }
    }

    [RelayCommand]
    public void ConfirmAyahModification(WizardAyahItemViewModel? item)
    {
        item?.ConfirmModification();
    }

    [RelayCommand]
    public async Task ImportCustomBackgroundAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "اختر صورة الخلفية",
            Filter = "صور (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
        };

        if (dlg.ShowDialog() == true)
        {
            var assetService = new AssetManagementService(_workspaceService);
            var asset = await assetService.ImportAssetAsync(dlg.FileName, UserAssetType.Background);
            if (asset != null)
            {
                WizardBackground = BackgroundType.CustomImage;
                WizardCustomBackgroundPath = asset.RelativePath;
                UpdateWizardPreview();
            }
        }
    }

    [RelayCommand]
    public async Task ImportWatermarkLogoAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "اختر شعار القناة (PNG شفاف)",
            Filter = "شعار PNG (*.png)|*.png"
        };

        if (dlg.ShowDialog() == true)
        {
            var assetService = new AssetManagementService(_workspaceService);
            var asset = await assetService.ImportAssetAsync(dlg.FileName, UserAssetType.Logo);
            if (asset != null)
            {
                WizardWatermark = WatermarkType.Logo;
                WizardWatermarkImagePath = asset.RelativePath;
                UpdateWizardPreview();
            }
        }
    }

    public IReadOnlyList<SurahInfo> AllSurahs => SurahCatalogService.GetAllSurahs();

    public string CurrentPreviewAyahDescription =>
        WizardAyatItems.Count > 0 && PreviewAyahIndex >= 0 && PreviewAyahIndex < WizardAyatItems.Count
            ? $"الآية {WizardAyatItems[PreviewAyahIndex].AyahNumber} ({PreviewAyahIndex + 1} من {WizardAyatItems.Count})"
            : string.Empty;

    public void UpdateWizardPreview()
    {
        if (WizardAyatItems.Count == 0) return;

        int index = Math.Clamp(PreviewAyahIndex, 0, WizardAyatItems.Count - 1);
        var item = WizardAyatItems[index];

        OnPropertyChanged(nameof(CurrentPreviewAyahDescription));

        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath)) return;

        var (targetW, targetH) = WizardOrientation == VideoOrientation.Horizontal ? (1920, 1080) : (1080, 1920);

        string? customBg = !string.IsNullOrWhiteSpace(WizardCustomBackgroundPath)
            ? _workspaceService.ToAbsolutePath(WizardCustomBackgroundPath)
            : null;

        string? watermarkLogo = !string.IsNullOrWhiteSpace(WizardWatermarkImagePath)
            ? _workspaceService.ToAbsolutePath(WizardWatermarkImagePath)
            : null;

        var settings = new CardCompositionSettings(
            TargetWidth: targetW,
            TargetHeight: targetH,
            BackgroundType: WizardBackground,
            CustomBackgroundPath: customBg,
            WatermarkType: WizardWatermark,
            WatermarkImagePath: watermarkLogo,
            WatermarkText: WizardWatermarkText,
            WatermarkPosition: WizardWatermarkPosition,
            WatermarkOpacity: WizardWatermarkOpacity,
            WatermarkScale: WizardWatermarkScale);

        try
        {
            WizardPreviewImage = AyahCardComposer.ComposeCardToBitmapSource(item.ImagePath, settings);
        }
        catch
        {
            // Ignore preview render exception
        }
    }

    [RelayCommand]
    public void PreviewNextAyah()
    {
        if (WizardAyatItems.Count == 0) return;
        if (PreviewAyahIndex < WizardAyatItems.Count - 1)
        {
            PreviewAyahIndex++;
            UpdateWizardPreview();
        }
    }

    [RelayCommand]
    public void PreviewPreviousAyah()
    {
        if (WizardAyatItems.Count == 0) return;
        if (PreviewAyahIndex > 0)
        {
            PreviewAyahIndex--;
            UpdateWizardPreview();
        }
    }

    [RelayCommand]
    public void PlayCurrentPreviewAyahAudio()
    {
        if (WizardAyatItems.Count == 0) return;
        int index = Math.Clamp(PreviewAyahIndex, 0, WizardAyatItems.Count - 1);
        var item = WizardAyatItems[index];
        if (item.SelectedTake != null && !string.IsNullOrEmpty(_currentAudioPath) && File.Exists(_currentAudioPath))
        {
            Player.PlaySegment(item.SelectedTake.StartTimeSec, item.SelectedTake.EndTimeSec);
        }
    }

    [RelayCommand]
    public void StopPreviewAudio()
    {
        Player.Stop();
    }

    [RelayCommand]
    public async Task SaveProjectAsync()
    {
        if (CurrentSurah == null || WizardAyatItems.Count == 0) return;

        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        VideoProjectEntity? project = null;
        if (ActiveProjectId.HasValue)
        {
            project = await db.Projects
                .Include(p => p.SelectedAyahs)
                .FirstOrDefaultAsync(p => p.Id == ActiveProjectId.Value);
        }

        if (project == null)
        {
            project = new VideoProjectEntity
            {
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Projects.Add(project);
        }

        project.Title = string.IsNullOrWhiteSpace(ProjectTitle)
            ? $"سورة {CurrentSurah.NameArabic} ({WizardStartAyah}-{WizardEndAyah})"
            : ProjectTitle;

        project.SurahNumber = CurrentSurah.Number;
        project.StartAyahNumber = WizardStartAyah;
        project.EndAyahNumber = WizardEndAyah;
        project.ReciterId = SelectedRecitation?.ReciterId ?? (string.IsNullOrWhiteSpace(project.ReciterId) ? "saber_abdelhakam" : project.ReciterId);
        project.AudioSourceId = CurrentAudioSourceId;
        project.Orientation = WizardOrientation == VideoOrientation.Horizontal ? "Horizontal" : "Vertical";
        project.Background = WizardBackground;
        project.CustomBackgroundPath = WizardCustomBackgroundPath;
        project.Watermark = WizardWatermark;
        project.WatermarkText = WizardWatermarkText;
        project.WatermarkImagePath = WizardWatermarkImagePath;
        project.WatermarkPosition = WizardWatermarkPosition;
        project.WatermarkOpacity = WizardWatermarkOpacity;
        project.WatermarkScale = WizardWatermarkScale;
        project.UpdatedAtUtc = DateTime.UtcNow;

        project.SelectedAyahs.Clear();
        int order = 1;
        foreach (var item in WizardAyatItems)
        {
            int passageId = item.SelectedTake?.Id ?? 0;
            int passageRevision = item.SelectedTake != null
                ? ((item.HasUnconfirmedModification && !item.IsModificationConfirmedByUser && item.SavedPassageRevision > 0)
                    ? item.SavedPassageRevision
                    : item.SelectedTake.Revision)
                : 0;

            double startTime = item.SelectedTake != null
                ? ((item.HasUnconfirmedModification && !item.IsModificationConfirmedByUser && item.SavedPassageRevision > 0)
                    ? item.SavedStartTimeSec
                    : item.SelectedTake.StartTimeSec)
                : 0;

            double endTime = item.SelectedTake != null
                ? ((item.HasUnconfirmedModification && !item.IsModificationConfirmedByUser && item.SavedPassageRevision > 0)
                    ? item.SavedEndTimeSec
                    : item.SelectedTake.EndTimeSec)
                : 0;

            int imageRev = (item.HasUnconfirmedModification && !item.IsModificationConfirmedByUser && item.SavedImageRevision > 0)
                ? item.SavedImageRevision
                : item.ImageRevision;

            int imageRecipeId = (item.HasUnconfirmedModification && !item.IsModificationConfirmedByUser && item.SavedImageRecipeId > 0)
                ? item.SavedImageRecipeId
                : item.ImageRecipeId;

            project.SelectedAyahs.Add(new ProjectAyahEntity
            {
                OrderIndex = order++,
                SurahNumber = CurrentSurah.Number,
                AyahNumber = item.AyahNumber,
                AyahId = item.AyahId,
                PassageId = passageId,
                PassageRevision = passageRevision,
                StartTimeSec = startTime,
                EndTimeSec = endTime,
                ImageRecipeId = imageRecipeId,
                ImageRevision = imageRev,
                JunctionReviewed = item.JunctionReviewed,
                RelativeImagePath = !string.IsNullOrEmpty(item.ImagePath)
                    ? Path.GetRelativePath(_workspaceService.WorkspacePath, item.ImagePath)
                    : string.Empty
            });
        }

        await db.SaveChangesAsync();
        ActiveProjectId = project.Id;

        await LoadSavedProjectsAsync();
    }

    private CancellationTokenSource? _projectExportCts;

    [RelayCommand]
    public void CancelProjectExport()
    {
        _projectExportCts?.Cancel();
    }

    [RelayCommand]
    public async Task ExportProjectAsync()
    {
        await SaveProjectAsync();
        if (!ActiveProjectId.HasValue) return;

        IsProjectExporting = true;
        ProjectExportProgress = 10;
        ProjectExportError = null;
        _projectExportCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<int>(pct => ProjectExportProgress = pct);

            var (ok, err, _) = await _compositionService.RenderProjectAsync(
                ActiveProjectId.Value,
                progress,
                _projectExportCts.Token);

            if (!ok)
            {
                ProjectExportError = err;
            }
            else
            {
                ProjectExportProgress = 100;
                await LoadSavedProjectsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            ProjectExportError = "تم إلغاء التصدير بناءً على طلب المستخدم.";
        }
        catch (Exception ex)
        {
            ProjectExportError = $"حدث خطأ أثناء التصدير: {ex.Message}";
        }
        finally
        {
            IsProjectExporting = false;
            _projectExportCts = null;
        }
    }

    [RelayCommand]
    public async Task LoadSavedProjectsAsync()
    {
        SavedProjects.Clear();
        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        var projects = await db.Projects
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        foreach (var p in projects)
        {
            SavedProjects.Add(p);
        }
    }

    [RelayCommand]
    public async Task OpenSavedProjectAsync(VideoProjectEntity project)
    {
        if (project == null) return;

        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        // Load project entity with its full saved ayahs and junction flags
        var fullProject = await db.Projects
            .Include(p => p.SelectedAyahs)
            .FirstOrDefaultAsync(p => p.Id == project.Id) ?? project;

        ActiveProjectId = fullProject.Id;
        ProjectTitle = fullProject.Title;
        WizardStartAyah = fullProject.StartAyahNumber;
        WizardEndAyah = fullProject.EndAyahNumber;
        WizardOrientation = fullProject.Orientation == "Vertical" ? VideoOrientation.Vertical : VideoOrientation.Horizontal;
        WizardBackground = fullProject.Background;
        WizardCustomBackgroundPath = fullProject.CustomBackgroundPath;
        WizardWatermark = fullProject.Watermark;
        WizardWatermarkText = fullProject.WatermarkText ?? string.Empty;
        WizardWatermarkImagePath = fullProject.WatermarkImagePath;
        WizardWatermarkPosition = fullProject.WatermarkPosition;
        WizardWatermarkOpacity = fullProject.WatermarkOpacity;
        WizardWatermarkScale = fullProject.WatermarkScale > 0 ? fullProject.WatermarkScale : 1.0;

        // Restore Reciter and AudioSource
        if (!string.IsNullOrEmpty(fullProject.ReciterId))
        {
            var matchReciter = Reciters.FirstOrDefault(r => r.ReciterId == fullProject.ReciterId);
            if (matchReciter != null)
            {
                SelectedRecitation = matchReciter;
            }
        }

        if (!string.IsNullOrEmpty(fullProject.AudioSourceId))
        {
            CurrentAudioSourceId = fullProject.AudioSourceId;
        }

        // Switch to this project's surah
        var surah = SurahCatalogService.GetSurah(fullProject.SurahNumber);
        if (surah != null)
        {
            CurrentSurah = surah;
        }

        await RefreshWizardSequenceAsync();

        // Reapply exact saved PassageIds and junction reviews (R01, R02)
        if (fullProject.SelectedAyahs.Count > 0)
        {
            var savedMap = fullProject.SelectedAyahs.ToDictionary(a => a.AyahNumber);
            foreach (var item in WizardAyatItems)
            {
                if (savedMap.TryGetValue(item.AyahNumber, out var savedAyah))
                {
                    item.SavedPassageId = savedAyah.PassageId;
                    item.SavedPassageRevision = savedAyah.PassageRevision;
                    item.SavedStartTimeSec = savedAyah.StartTimeSec;
                    item.SavedEndTimeSec = savedAyah.EndTimeSec;
                    item.SavedImageRecipeId = savedAyah.ImageRecipeId;
                    item.SavedImageRevision = savedAyah.ImageRevision;

                    var matchingTake = item.AvailableTakes.FirstOrDefault(t => t.Id == savedAyah.PassageId);
                    if (matchingTake != null)
                    {
                        item.SelectedTake = matchingTake;

                        bool audioModified = savedAyah.PassageRevision > 0 &&
                            (matchingTake.Revision != savedAyah.PassageRevision ||
                             Math.Abs(matchingTake.StartTimeSec - savedAyah.StartTimeSec) > 0.05 ||
                             Math.Abs(matchingTake.EndTimeSec - savedAyah.EndTimeSec) > 0.05);

                        bool imageModified = savedAyah.ImageRevision > 0 &&
                            (item.ImageRevision != savedAyah.ImageRevision || item.ImageRecipeId != savedAyah.ImageRecipeId);

                        if (audioModified || imageModified)
                        {
                            item.HasUnconfirmedModification = true;
                            item.IsModificationConfirmedByUser = false;
                            item.JunctionReviewed = false;
                        }
                        else
                        {
                            item.HasUnconfirmedModification = false;
                            item.IsModificationConfirmedByUser = true;
                            item.JunctionReviewed = savedAyah.JunctionReviewed;
                        }
                    }
                    else
                    {
                        // R01: Saved take is missing! Do not automatically select another take!
                        item.SelectedTake = null;
                        item.HasMissingSavedTake = true;
                        item.JunctionReviewed = false;
                    }
                }
            }

            RecalculateGapsAndStatus();
        }

        GoToWizardStep(1);
    }

    [RelayCommand]
    public async Task DeleteSavedProjectAsync(VideoProjectEntity project)
    {
        if (project == null) return;

        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        var existing = await db.Projects.FindAsync(project.Id);
        if (existing != null)
        {
            db.Projects.Remove(existing);
            await db.SaveChangesAsync();
        }

        if (ActiveProjectId == project.Id)
        {
            ActiveProjectId = null;
        }

        await LoadSavedProjectsAsync();
    }
}
