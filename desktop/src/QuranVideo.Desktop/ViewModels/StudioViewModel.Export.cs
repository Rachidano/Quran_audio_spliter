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
/// Studio — export individuel : orientation, arrière-plan, génération MP4/MP3
/// et enregistrement dans « فيديوهاتي ».
/// </summary>
public sealed partial class StudioViewModel
{
    private CancellationTokenSource? _videoCts;

    [ObservableProperty]
    private bool _isHorizontal = true;

    public bool IsVertical
    {
        get => !IsHorizontal;
        set
        {
            if (value != !IsHorizontal)
            {
                IsHorizontal = !value;
            }
        }
    }

    partial void OnIsHorizontalChanged(bool value)
    {
        OnPropertyChanged(nameof(IsVertical));
        OnPropertyChanged(nameof(OrientationSummary));
        OnPropertyChanged(nameof(OrientationIndex));
    }

    [ObservableProperty]
    private BackgroundType _studioBackground = BackgroundType.Light;

    [ObservableProperty]
    private string? _studioCustomBackgroundPath;

    public bool IsStudioBackgroundLight => StudioBackground == BackgroundType.Light;
    public bool IsStudioBackgroundDark => StudioBackground == BackgroundType.Dark;
    public bool IsStudioBackgroundCustom => StudioBackground == BackgroundType.CustomImage;
    public string StudioCustomBackgroundFileName => !string.IsNullOrWhiteSpace(StudioCustomBackgroundPath) 
        ? Path.GetFileName(StudioCustomBackgroundPath) 
        : string.Empty;

    partial void OnStudioBackgroundChanged(BackgroundType value)
    {
        OnPropertyChanged(nameof(IsStudioBackgroundLight));
        OnPropertyChanged(nameof(IsStudioBackgroundDark));
        OnPropertyChanged(nameof(IsStudioBackgroundCustom));
        OnPropertyChanged(nameof(BackgroundSummary));
        OnPropertyChanged(nameof(BackgroundIndex));
    }

    partial void OnStudioCustomBackgroundPathChanged(string? value)
    {
        OnPropertyChanged(nameof(StudioCustomBackgroundFileName));
        OnPropertyChanged(nameof(BackgroundSummary));
    }

    [RelayCommand]
    public void SetStudioBackground(string bg)
    {
        StudioBackground = bg switch
        {
            "Dark" => BackgroundType.Dark,
            "Custom" or "CustomImage" => BackgroundType.CustomImage,
            _ => BackgroundType.Light
        };
    }

    [RelayCommand]
    public async Task ImportStudioCustomBackgroundAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر صورة الخلفية للفيديو",
            Filter = "صور (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
        };

        if (dlg.ShowDialog() == true)
        {
            var assetService = new AssetManagementService(_workspaceService);
            var asset = await assetService.ImportAssetAsync(dlg.FileName, UserAssetType.Background);
            if (asset != null)
            {
                StudioBackground = BackgroundType.CustomImage;
                StudioCustomBackgroundPath = asset.RelativePath;
            }
        }
    }

    [RelayCommand]
    public void ClearStudioCustomBackground()
    {
        StudioCustomBackgroundPath = null;
        StudioBackground = BackgroundType.Light;
    }

    [ObservableProperty]
    private bool _isGenerating;

    public bool IsNotGenerating => !IsGenerating;

    partial void OnIsGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotGenerating));
    }

    [ObservableProperty]
    private int _generationProgress;

    [ObservableProperty]
    private string? _lastGeneratedVideoPath;

    [RelayCommand]
    private async Task GenerateVideoAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "جارٍ تحضير تكوين الفيديو...";
        GenerationProgress = 10;

        _videoCts = new CancellationTokenSource();

        try
        {
            var orientation = IsHorizontal ? VideoOrientation.Horizontal : VideoOrientation.Vertical;
            var (prepOk, prepError, comp) = await _compositionService.PrepareCompositionAsync(
                CurrentSurah.Number,
                CurrentAyahNumber,
                orientation, CurrentReciterId, CurrentCollectionId);

            if (!prepOk || comp == null)
            {
                StatusMessage = $"تعذر التوليد: {prepError}";
                IsGenerating = false;
                return;
            }

            StatusMessage = "جارٍ معالجة الفيديو وإنشاء ملف MP4 بواسطة FFmpeg...";
            var progress = new Progress<int>(p => GenerationProgress = p);

            var (targetW, targetH) = IsHorizontal ? (1920, 1080) : (1080, 1920);
            string? customBg = !string.IsNullOrWhiteSpace(StudioCustomBackgroundPath)
                ? _workspaceService.ToAbsolutePath(StudioCustomBackgroundPath)
                : null;

            string? watermarkLogo = !string.IsNullOrWhiteSpace(WizardWatermarkImagePath)
                ? _workspaceService.ToAbsolutePath(WizardWatermarkImagePath)
                : null;

            var settings = new CardCompositionSettings(
                TargetWidth: targetW,
                TargetHeight: targetH,
                BackgroundType: StudioBackground,
                CustomBackgroundPath: customBg,
                WatermarkType: WizardWatermark,
                WatermarkImagePath: watermarkLogo,
                WatermarkText: WizardWatermarkText,
                WatermarkPosition: WizardWatermarkPosition,
                WatermarkOpacity: WizardWatermarkOpacity,
                WatermarkScale: WizardWatermarkScale);

            var (renderOk, renderError, completedComp) = await _compositionService.RenderCompositionAsync(
                comp.Id,
                settings,
                progress,
                _videoCts.Token);

            if (renderOk)
            {
                var fullVideoPath = Path.Combine(_workspaceService.WorkspacePath, completedComp.RelativeVideoPath);
                LastGeneratedVideoPath = fullVideoPath;
                StatusMessage = $"تم إنشاء الفيديو بنجاح ({completedComp.Width}x{completedComp.Height} — {completedComp.DurationSeconds:F1} ثانية) مع إصدار البيان (Manifest)";
                LoggingService.Instance.LogInfo($"Vidéo générée avec succès : {fullVideoPath}");

                try
                {
                    await SaveStudioExportProjectAsync(completedComp, customBg, watermarkLogo);
                }
                catch (Exception pEx)
                {
                    LoggingService.Instance.LogError("Erreur création entité projet pour vidéo studio", pEx);
                }
            }
            else
            {
                StatusMessage = $"فشل إنشاء الفيديو : {renderError}";
                LoggingService.Instance.LogError($"Échec génération vidéo : {renderError}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "تم إلغاء عملية إنشاء الفيديو.";
            LoggingService.Instance.LogInfo("Génération vidéo annulée par l'utilisateur.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ غير متوقع : {ex.Message}";
            LoggingService.Instance.LogError("Erreur inattendue création vidéo", ex);
        }
        finally
        {
            IsGenerating = false;
            _videoCts?.Dispose();
            _videoCts = null;
        }
    }

    [RelayCommand]
    private async Task ExtractAudioOnlyAsync()
    {
        if (IsGenerating) return;

        if (string.IsNullOrEmpty(_currentAudioPath) || !File.Exists(_currentAudioPath))
        {
            StatusMessage = "تعذر الاستخراج: ملف التلاوة الصوتية لهذه السورة غير متوفر محلياً.";
            return;
        }

        if (EndTimeSec <= StartTimeSec)
        {
            StatusMessage = "تعذر الاستخراج: وقت نهاية المقطع يجب أن يكون أكبر من وقت البداية.";
            return;
        }

        IsGenerating = true;
        StatusMessage = "جارٍ استخراج المقطع الصوتي MP3 بواسطة FFmpeg...";
        GenerationProgress = 20;

        _videoCts = new CancellationTokenSource();

        try
        {
            var outputDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
            Directory.CreateDirectory(outputDir);
            var fileName = $"ayah_{CurrentSurah.Number:D3}_{CurrentAyahNumber:D3}_audio.mp3";
            var outputMp3Path = Path.Combine(outputDir, fileName);

            GenerationProgress = 50;

            var result = await FfmpegVideoGenerator.ExtractAyahAudioAsync(
                _currentAudioPath,
                StartTimeSec,
                EndTimeSec,
                outputMp3Path,
                _videoCts.Token);

            if (result.Success && File.Exists(outputMp3Path))
            {
                GenerationProgress = 100;
                LastGeneratedVideoPath = outputMp3Path;
                StatusMessage = $"تم استخراج ملف الصوت بنجاح: {fileName} 🎵";
                LoggingService.Instance.LogInfo($"Audio MP3 extrait avec succès : {outputMp3Path}");
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "فشل استخراج المقطع الصوتي بواسطة FFmpeg.";
                LoggingService.Instance.LogError($"Échec extraction audio MP3 : {outputMp3Path}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "تم إلغاء عملية استخراج الصوت.";
            LoggingService.Instance.LogInfo("Extraction audio annulée par l'utilisateur.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ غير متوقع: {ex.Message}";
            LoggingService.Instance.LogError("Erreur inattendue extraction audio", ex);
        }
        finally
        {
            IsGenerating = false;
            _videoCts?.Dispose();
            _videoCts = null;
        }
    }



    [RelayCommand]
    private void CancelGeneration()
    {
        _videoCts?.Cancel();
    }

    /// <summary>
    /// Enregistre (ou met à jour) l'entrée « فيديوهاتي » d'une vidéo exportée depuis le studio.
    /// Seul le projet créé par un export studio précédent (vidéo "ayah_...") pour la même
    /// source audio est réutilisé : les projets du wizard ("project_p...") et ceux d'un
    /// autre récitateur ne sont jamais écrasés.
    /// </summary>
    public async Task SaveStudioExportProjectAsync(VideoCompositionEntity completedComp, string? customBg, string? watermarkLogo)
    {
        using var db = new AppDbContext(_workspaceService.GetDatabasePath());
        var surahNumber = completedComp.SurahNumber;
        var ayahNumber = completedComp.AyahNumber;
        var surahName = SurahCatalogService.GetSurah(surahNumber)?.NameArabic ?? $"سورة {surahNumber}";

        var candidates = await db.Projects
            .Include(p => p.SelectedAyahs)
            .Where(p =>
                p.SurahNumber == surahNumber &&
                p.StartAyahNumber == ayahNumber &&
                p.EndAyahNumber == ayahNumber &&
                p.Orientation == completedComp.Orientation &&
                p.AudioSourceId == completedComp.AudioSourceId)
            .ToListAsync();
        var project = candidates.FirstOrDefault(p =>
            Path.GetFileName(p.OutputVideoPath ?? string.Empty).StartsWith("ayah_", StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            project = new VideoProjectEntity
            {
                SurahNumber = surahNumber,
                StartAyahNumber = ayahNumber,
                EndAyahNumber = ayahNumber,
                Orientation = completedComp.Orientation,
                CreatedAtUtc = DateTime.UtcNow,
                SelectedAyahs = new List<ProjectAyahEntity>()
            };
            db.Projects.Add(project);
        }

        project.Title = $"{surahName} — الآية {ayahNumber}";
        project.ReciterId = CurrentReciterId;
        project.AudioSourceId = completedComp.AudioSourceId;
        project.Background = StudioBackground;
        project.CustomBackgroundPath = customBg;
        project.Watermark = WizardWatermark;
        project.WatermarkText = WizardWatermarkText;
        project.WatermarkImagePath = watermarkLogo;
        project.WatermarkPosition = WizardWatermarkPosition;
        project.WatermarkOpacity = WizardWatermarkOpacity;
        project.WatermarkScale = WizardWatermarkScale;
        project.IsWatermarkEnabled = WizardWatermark != WatermarkType.None;
        project.Status = "Completed";
        project.OutputVideoPath = completedComp.RelativeVideoPath;
        project.LastRenderedAtUtc = DateTime.UtcNow;
        project.UpdatedAtUtc = DateTime.UtcNow;

        project.SelectedAyahs ??= new List<ProjectAyahEntity>();
        project.SelectedAyahs.Clear();
        project.SelectedAyahs.Add(new ProjectAyahEntity
        {
            OrderIndex = 0,
            SurahNumber = surahNumber,
            AyahNumber = ayahNumber,
            AyahId = $"{surahNumber}:{ayahNumber}",
            PassageId = completedComp.PassageId,
            PassageRevision = completedComp.PassageRevision,
            StartTimeSec = completedComp.StartTimeSec,
            EndTimeSec = completedComp.EndTimeSec,
            ImageRecipeId = completedComp.ImageRecipeId,
            ImageRevision = completedComp.ImageRevision,
            RelativeImagePath = completedComp.ImagePath,
            JunctionReviewed = true
        });

        await db.SaveChangesAsync();
    }
}
