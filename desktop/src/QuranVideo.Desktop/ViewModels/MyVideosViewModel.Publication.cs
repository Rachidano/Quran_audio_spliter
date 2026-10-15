using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

/// <summary>
/// « فيديوهاتي » — titre et description de publication de la vidéo sélectionnée :
/// affichage, copie dans le presse-papiers et (re)génération.
/// </summary>
public sealed partial class MyVideosViewModel
{
    /// <summary>Copie dans le presse-papiers (remplaçable dans les tests, où il n'y a pas de presse-papiers).</summary>
    public static Action<string> ClipboardSetter { get; set; } = text => System.Windows.Clipboard.SetText(text);

    [ObservableProperty] private string _publicationTitle = string.Empty;
    [ObservableProperty] private string _publicationDescription = string.Empty;
    [ObservableProperty] private bool _hasPublicationText;
    [ObservableProperty] private string _publicationStatusMessage = string.Empty;
    [ObservableProperty] private bool _isGeneratingPublicationText;

    /// <summary>Le panneau ne concerne que les vidéos (pas les extraits audio).</summary>
    public bool IsPublicationPanelVisible => SelectedVideo?.IsMp4 == true;

    public string GeneratePublicationTextLabel => HasPublicationText ? "🔄 إعادة إنشاء النص" : "✨ إنشاء النص";

    partial void OnHasPublicationTextChanged(bool value) => OnPropertyChanged(nameof(GeneratePublicationTextLabel));

    /// <summary>Recharge le texte de la vidéo sélectionnée depuis son fichier .txt.</summary>
    public void RefreshPublicationText()
    {
        OnPropertyChanged(nameof(IsPublicationPanelVisible));
        PublicationStatusMessage = string.Empty;

        var text = SelectedVideo?.IsMp4 == true ? PublicationTextService.Read(SelectedVideo.FullPath) : null;
        HasPublicationText = text != null;
        PublicationTitle = text?.Title ?? string.Empty;
        PublicationDescription = text?.Description ?? string.Empty;

        if (SelectedVideo?.IsMp4 == true && text == null)
            PublicationStatusMessage = "لا يوجد نص نشر لهذا الفيديو بعد. اضغط «إنشاء النص».";
    }

    [RelayCommand]
    public void CopyPublicationTitle() => Copy(PublicationTitle, "تم نسخ العنوان ✔");

    [RelayCommand]
    public void CopyPublicationDescription() => Copy(PublicationDescription, "تم نسخ الوصف ✔");

    private void Copy(string text, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            ClipboardSetter(text);
            PublicationStatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            PublicationStatusMessage = $"تعذر النسخ: {ex.Message}";
            LoggingService.Instance.LogWarning($"Copie presse-papiers impossible : {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task GeneratePublicationTextAsync()
    {
        var video = SelectedVideo;
        if (video == null || !video.IsMp4 || IsGeneratingPublicationText) return;

        IsGeneratingPublicationText = true;
        try
        {
            var path = await new PublicationTextService(_workspaceService).GenerateForExistingVideoAsync(video.FullPath);
            if (SelectedVideo?.FullPath != video.FullPath) return;

            RefreshPublicationText();
            PublicationStatusMessage = path != null
                ? "تم إنشاء نص النشر ✔"
                : "تعذر تحديد السورة والآيات لهذا الملف (ربما غُيّر اسمه). أعد تصديره من التطبيق.";
        }
        catch (Exception ex)
        {
            PublicationStatusMessage = $"تعذر إنشاء النص: {ex.Message}";
            LoggingService.Instance.LogError($"Génération du texte de publication pour {video.FullPath}", ex);
        }
        finally
        {
            IsGeneratingPublicationText = false;
        }
    }

    /// <summary>Renomme le texte de publication avec la vidéo pour garder le lien entre les deux fichiers.</summary>
    private static void MovePublicationText(string oldVideoPath, string newVideoPath)
    {
        var oldText = PublicationTextService.GetTextPath(oldVideoPath);
        var newText = PublicationTextService.GetTextPath(newVideoPath);
        if (File.Exists(oldText) && !File.Exists(newText)) File.Move(oldText, newText);
    }
}
