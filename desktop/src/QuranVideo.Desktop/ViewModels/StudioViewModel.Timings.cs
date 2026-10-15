using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

/// <summary>
/// Import des minutages ayah par ayah publiés (mp3quran.net) comme prises « مقترح ».
/// </summary>
public sealed partial class StudioViewModel
{
    /// <summary>Le récitateur sélectionné dispose de minutages publiés.</summary>
    [ObservableProperty] private bool _hasReadyTimings;

    [ObservableProperty] private bool _isImportingTimings;

    /// <summary>Décalage global (secondes) appliqué aux minutages importés, saisi par l'utilisateur.</summary>
    [ObservableProperty] private string _readyTimingsOffsetText = "0";

    private int _readyTimingsCheckVersion;

    public int ProposedPassagesCount => RecordedPassages.Count(p => p.IsProposedTiming);

    public async Task RefreshReadyTimingsAvailabilityAsync()
    {
        var version = ++_readyTimingsCheckVersion;
        var collectionId = CurrentCollectionId;
        try
        {
            var readId = await new AyahTimingImportService(_workspaceService).GetTimingReadIdAsync(collectionId);
            if (version == _readyTimingsCheckVersion) HasReadyTimings = readId.HasValue;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Disponibilité des minutages inconnue pour {collectionId} : {ex.Message}");
            if (version == _readyTimingsCheckVersion) HasReadyTimings = false;
        }
    }

    public static bool TryParseOffset(string? text, out double offsetSec)
    {
        var normalized = (text ?? string.Empty).Trim().Replace('٫', '.').Replace(',', '.');
        if (normalized.Length == 0)
        {
            offsetSec = 0;
            return true;
        }
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out offsetSec)
               && Math.Abs(offsetSec) <= 30;
    }

    /// <summary>
    /// Importe les minutages de la sourate courante. Les prises faites à la main sont conservées ;
    /// les prises encore « مقترح » sont réécrites (utile après un changement de décalage).
    /// </summary>
    [RelayCommand]
    public async Task ImportReadyTimingsAsync()
    {
        if (IsImportingTimings) return;
        if (!TryParseOffset(ReadyTimingsOffsetText, out var offset))
        {
            StatusMessage = "قيمة الإزاحة غير صالحة. أدخل رقماً بالثواني بين -30 و 30 (مثال: 0.3).";
            return;
        }

        IsImportingTimings = true;
        try
        {
            StatusMessage = "جارٍ استيراد التوقيتات الجاهزة...";
            var surah = CurrentSurah.Number;
            var collection = CurrentCollectionId;
            var result = await new AyahTimingImportService(_workspaceService)
                .ImportAsync(collection, surah, replaceProposed: true, offsetSec: offset);

            if (!result.Success)
            {
                StatusMessage = $"تعذر استيراد التوقيتات: {result.ErrorMessage}";
                return;
            }

            if (collection == CurrentCollectionId && surah == CurrentSurah.Number)
            {
                await RefreshPassagesListAsync();
                LoadPassageBoundsForCurrentAyah();
            }

            var summary = $"تم استيراد التوقيتات المقترحة: {result.ImportedCount} آية جديدة، {result.UpdatedCount} محدّثة، " +
                          $"{result.SkippedManualCount} محفوظة دون تغيير. استمع إلى الحدود وصحّح عند الحاجة.";
            StatusMessage = result.Warning == null ? summary : $"{summary}\n{result.Warning}";
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Import des minutages publiés", ex);
            StatusMessage = $"تعذر استيراد التوقيتات: {ex.Message}";
        }
        finally
        {
            IsImportingTimings = false;
        }
    }
}
