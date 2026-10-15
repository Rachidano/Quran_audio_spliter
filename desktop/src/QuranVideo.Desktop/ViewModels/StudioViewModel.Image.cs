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
/// Studio — image de l'ayah : page du Mushaf, image isolée et éditeur de marges.
/// </summary>
public sealed partial class StudioViewModel
{
    [ObservableProperty]
    private string? _currentPageImagePath;

    [ObservableProperty]
    private int _currentPageNumber = 1;

    [ObservableProperty]
    private string? _currentIsolatedImagePath;

    [ObservableProperty]
    private string? _comparisonImagePath;

    [ObservableProperty]
    private AyahImageStatus _currentImageStatus = AyahImageStatus.Suggested;

    [ObservableProperty]
    private bool _isImageEditorOpen;

    [ObservableProperty]
    private int _marginTop;

    [ObservableProperty]
    private int _marginBottom;

    [ObservableProperty]
    private int _marginLeft;

    [ObservableProperty]
    private int _marginRight;

    private void UpdateCurrentPageNumber()
    {
        // Query ayah page number from database if available
        try
        {
            var dbPath = _workspaceService.GetDatabasePath();
            if (File.Exists(dbPath))
            {
                using var db = new AppDbContext(dbPath);
                var ayahId = $"{CurrentSurah.Number}:{CurrentAyahNumber}";
                var ayah = db.Ayahs.FirstOrDefault(a => a.Id == ayahId);
                if (ayah != null && ayah.PageNumber > 0)
                {
                    CurrentPageNumber = ayah.PageNumber;
                }
                else
                {
                    CurrentPageNumber = CurrentSurah.StartPage;
                }
            }
            else
            {
                CurrentPageNumber = CurrentSurah.StartPage;
            }
        }
        catch
        {
            CurrentPageNumber = CurrentSurah.StartPage;
        }

        var resolved = _imageService.ResolvePageImagePath(CurrentPageNumber);
        CurrentPageImagePath = string.IsNullOrEmpty(resolved) ? null : resolved;
    }

    public async Task LoadAyahImageAsync()
    {
        try
        {
            var res = await _imageService.GetOrExtractAyahImageAsync(CurrentSurah.Number, CurrentAyahNumber);
            if (res.Success)
            {
                CurrentIsolatedImagePath = null;
                CurrentIsolatedImagePath = res.ImagePath;
                CurrentImageStatus = res.Recipe.Status;
                MarginTop = res.Recipe.MarginTop;
                MarginBottom = res.Recipe.MarginBottom;
                MarginLeft = res.Recipe.MarginLeft;
                MarginRight = res.Recipe.MarginRight;
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Impossible d'extraire l'image de l'ayah: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ToggleImageEditorAsync()
    {
        IsImageEditorOpen = !IsImageEditorOpen;
        if (IsImageEditorOpen)
        {
            ComparisonImagePath = await _imageService.GenerateSideBySideComparisonAsync(CurrentSurah.Number, CurrentAyahNumber);
        }
    }

    [RelayCommand]
    public async Task NudgeMarginAsync(string param)
    {
        int dTop = 0, dBottom = 0, dLeft = 0, dRight = 0;
        if (param == "top+5") dTop = 5;
        else if (param == "top-5") dTop = -5;
        else if (param == "bottom+5") dBottom = 5;
        else if (param == "bottom-5") dBottom = -5;
        else if (param == "right+5") dRight = 5;
        else if (param == "right-5") dRight = -5;
        else if (param == "left+5") dLeft = 5;
        else if (param == "left-5") dLeft = -5;

        var res = await _imageService.ApplyCorrectionAsync(CurrentSurah.Number, CurrentAyahNumber, dTop, dBottom, dLeft, dRight);
        if (res.Success)
        {
            CurrentIsolatedImagePath = res.ImagePath;
            CurrentImageStatus = res.Recipe.Status;
            MarginTop = res.Recipe.MarginTop;
            MarginBottom = res.Recipe.MarginBottom;
            MarginLeft = res.Recipe.MarginLeft;
            MarginRight = res.Recipe.MarginRight;
            ComparisonImagePath = await _imageService.GenerateSideBySideComparisonAsync(CurrentSurah.Number, CurrentAyahNumber);
            StatusMessage = $"تم تعديل هوامش صورة الآية (مراجعة {res.Recipe.Revision})";
        }
    }

    [RelayCommand]
    public async Task ResetImageCorrectionAsync()
    {
        var res = await _imageService.ResetCorrectionAsync(CurrentSurah.Number, CurrentAyahNumber);
        if (res.Success)
        {
            CurrentIsolatedImagePath = res.ImagePath;
            CurrentImageStatus = res.Recipe.Status;
            MarginTop = 0;
            MarginBottom = 0;
            MarginLeft = 0;
            MarginRight = 0;
            ComparisonImagePath = await _imageService.GenerateSideBySideComparisonAsync(CurrentSurah.Number, CurrentAyahNumber);
            StatusMessage = "تمت إعادة ضبط هوامش الصورة للاستخراج التلقائي.";
        }
    }
}
