using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.ViewModels;

/// <summary>
/// Studio — fenêtre « تهيئة الفيديو » : ouverture/fermeture et résumé des réglages
/// (orientation, arrière-plan, signature) affiché dans la carte de création.
/// </summary>
public sealed partial class StudioViewModel
{
    /// <summary>Fenêtre de réglages ouverte : les raccourcis clavier du studio sont alors suspendus.</summary>
    [ObservableProperty] private bool _isVideoSettingsOpen;

    [RelayCommand]
    public void OpenVideoSettings() => IsVideoSettingsOpen = true;

    [RelayCommand]
    public void CloseVideoSettings() => IsVideoSettingsOpen = false;

    public string OrientationSummary => IsHorizontal ? "📐 أفقي 16:9" : "📱 عمودي 9:16";

    public string BackgroundSummary => StudioBackground switch
    {
        BackgroundType.Dark => "🌑 خلفية داكنة",
        BackgroundType.CustomImage => string.IsNullOrWhiteSpace(StudioCustomBackgroundFileName)
            ? "🖼️ صورة خلفية"
            : $"🖼️ {StudioCustomBackgroundFileName}",
        _ => "⚪ خلفية فاتحة"
    };

    public string WatermarkSummary => WizardWatermark switch
    {
        WatermarkType.Text => string.IsNullOrWhiteSpace(WizardWatermarkText)
            ? "✍️ توقيع نصي (فارغ)"
            : $"✍️ {WizardWatermarkText.Trim()}",
        WatermarkType.Logo => string.IsNullOrWhiteSpace(WatermarkLogoFileName)
            ? "🏷️ شعار"
            : $"🏷️ {WatermarkLogoFileName}",
        _ => "🚫 بدون توقيع"
    };

    public bool HasWatermark => WizardWatermark != WatermarkType.None;
    public bool IsWatermarkText => WizardWatermark == WatermarkType.Text;
    public bool IsWatermarkLogo => WizardWatermark == WatermarkType.Logo;

    public string WatermarkLogoFileName => string.IsNullOrWhiteSpace(WizardWatermarkImagePath)
        ? string.Empty
        : Path.GetFileName(WizardWatermarkImagePath);

    partial void OnWizardWatermarkChanged(WatermarkType value) => RaiseWatermarkSummaryChanged();

    partial void OnWizardWatermarkImagePathChanged(string? value) => RaiseWatermarkSummaryChanged();

    private void RaiseWatermarkSummaryChanged()
    {
        OnPropertyChanged(nameof(WatermarkSummary));
        OnPropertyChanged(nameof(HasWatermark));
        OnPropertyChanged(nameof(IsWatermarkText));
        OnPropertyChanged(nameof(IsWatermarkLogo));
        OnPropertyChanged(nameof(WatermarkLogoFileName));
    }
}
