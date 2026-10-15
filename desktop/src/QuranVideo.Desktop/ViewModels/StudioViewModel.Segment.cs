using System.Globalization;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.ViewModels;

/// <summary>
/// Studio — panneau « معلومات المقطع » (saisie des temps au clavier) et listes déroulantes
/// de la carte « إنشاء الفيديو » (format, arrière-plan, type de signature).
/// </summary>
public sealed partial class StudioViewModel
{
    // ---------------------------------------------------------------- Saisie des temps

    /// <summary>Début du segment, modifiable au clavier (mm:ss.ff, h:mm:ss.ff ou secondes).</summary>
    public string StartTimeText
    {
        get => FormatTime(StartTimeSec);
        set => ApplyTypedTime(value, isStart: true);
    }

    /// <summary>Fin du segment, modifiable au clavier (mm:ss.ff, h:mm:ss.ff ou secondes).</summary>
    public string EndTimeText
    {
        get => FormatTime(EndTimeSec);
        set => ApplyTypedTime(value, isStart: false);
    }

    public string SegmentDurationText => FormatTime(Math.Max(0, EndTimeSec - StartTimeSec));

    private void ApplyTypedTime(string text, bool isStart)
    {
        if (!TryParseTime(text, out var seconds) || (AudioDurationSec > 0 && seconds > AudioDurationSec + 0.5))
        {
            StatusMessage = "صيغة الوقت غير صحيحة. اكتب مثلاً 00:46.52 أو 1:02:05.30 أو 46.52";
        }
        else if (isStart)
        {
            StartTimeSec = Math.Round(seconds, 2);
        }
        else
        {
            EndTimeSec = Math.Round(seconds, 2);
        }

        // Réafficher la valeur retenue (ou l'ancienne si la saisie est refusée)
        OnPropertyChanged(isStart ? nameof(StartTimeText) : nameof(EndTimeText));
    }

    /// <summary>Accepte « 46.52 », « 0:46.52 », « 00:46,52 », « 1:02:05.3 » (virgule arabe acceptée).</summary>
    public static bool TryParseTime(string? text, out double seconds)
    {
        seconds = 0;
        var normalized = (text ?? string.Empty).Trim().Replace('٫', '.').Replace(',', '.');
        if (normalized.Length == 0) return false;

        var parts = normalized.Split(':');
        if (parts.Length > 3) return false;

        double total = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var isLast = i == parts.Length - 1;
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
                return false;
            if (!isLast && value != Math.Floor(value)) return false;   // seules les secondes ont une partie décimale
            if (i > 0 && value >= 60) return false;                     // minutes et secondes < 60 après un « : »
            total = total * 60 + value;
        }

        seconds = total;
        return double.IsFinite(seconds);
    }

    private void RaiseSegmentTextsChanged()
    {
        OnPropertyChanged(nameof(StartTimeText));
        OnPropertyChanged(nameof(EndTimeText));
        OnPropertyChanged(nameof(SegmentDurationText));
    }

    // ---------------------------------------------------------------- Listes déroulantes vidéo

    /// <summary>0 = أفقي 16:9, 1 = عمودي 9:16.</summary>
    public int OrientationIndex
    {
        get => IsHorizontal ? 0 : 1;
        set => IsHorizontal = value != 1;
    }

    /// <summary>0 = فاتحة, 1 = داكنة, 2 = صورة (choisie dans une boîte de dialogue si aucune n'est encore définie).</summary>
    public int BackgroundIndex
    {
        get => StudioBackground switch { BackgroundType.Dark => 1, BackgroundType.CustomImage => 2, _ => 0 };
        set
        {
            if (value == BackgroundIndex) return;
            if (value == 2)
            {
                if (!string.IsNullOrWhiteSpace(StudioCustomBackgroundPath))
                    StudioBackground = BackgroundType.CustomImage;
                else
                    _ = ChooseCustomBackgroundFromListAsync();
                return;
            }
            SetStudioBackground(value == 1 ? "Dark" : "Light");
        }
    }

    private async Task ChooseCustomBackgroundFromListAsync()
    {
        await ImportStudioCustomBackgroundAsync();
        // Annulé : la liste revient au choix précédent
        OnPropertyChanged(nameof(BackgroundIndex));
    }

    /// <summary>0 = بدون, 1 = نص (ouvre « تهيئة » pour saisir le texte), 2 = شعار (choisi si aucun).</summary>
    public int WatermarkIndex
    {
        get => WizardWatermark switch { WatermarkType.Text => 1, WatermarkType.Logo => 2, _ => 0 };
        set
        {
            if (value == WatermarkIndex) return;
            switch (value)
            {
                case 1:
                    SetWatermarkType("Text");
                    if (string.IsNullOrWhiteSpace(WizardWatermarkText)) IsVideoSettingsOpen = true;
                    break;
                case 2:
                    if (!string.IsNullOrWhiteSpace(WizardWatermarkImagePath))
                        SetWatermarkType("Logo");
                    else
                        _ = ChooseLogoFromListAsync();
                    break;
                default:
                    SetWatermarkType("None");
                    break;
            }
        }
    }

    private async Task ChooseLogoFromListAsync()
    {
        await ImportWatermarkLogoAsync();
        OnPropertyChanged(nameof(WatermarkIndex));
    }
}
