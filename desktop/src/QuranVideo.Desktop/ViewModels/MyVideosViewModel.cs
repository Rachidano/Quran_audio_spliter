using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed record VideoFileInfo(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime CreatedAt)
{
    public string FormattedSize => $"{SizeBytes / (1024.0 * 1024.0):F2} ميغابايت";
    public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm");
}

public sealed partial class MyVideosViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;

    [ObservableProperty]
    private VideoFileInfo? _selectedVideo;

    public ObservableCollection<VideoFileInfo> Videos { get; } = new();

    public MyVideosViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        RefreshVideos();
    }

    [RelayCommand]
    public void RefreshVideos()
    {
        Videos.Clear();
        if (string.IsNullOrWhiteSpace(_workspaceService.WorkspacePath)) return;

        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        if (!Directory.Exists(videosDir)) return;

        var files = Directory.GetFiles(videosDir, "*.mp4")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach (var fi in files)
        {
            Videos.Add(new VideoFileInfo(fi.Name, fi.FullName, fi.Length, fi.CreationTime));
        }

        SelectedVideo = Videos.FirstOrDefault();
    }

    [RelayCommand]
    private void PlaySelectedVideo()
    {
        if (SelectedVideo != null && File.Exists(SelectedVideo.FullPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(SelectedVideo.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Impossible de lire la vidéo : {SelectedVideo.FullPath}", ex);
            }
        }
    }

    [RelayCommand]
    private void OpenVideosFolder()
    {
        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        Directory.CreateDirectory(videosDir);
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", videosDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Impossible d'ouvrir le dossier des vidéos", ex);
        }
    }
}
