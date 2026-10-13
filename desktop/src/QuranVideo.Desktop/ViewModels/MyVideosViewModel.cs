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

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _newVideoName = string.Empty;

    [ObservableProperty]
    private VideoFileInfo? _videoToRename;

    public bool HasVideos => Videos.Count > 0;
    public bool HasNoVideos => Videos.Count == 0;

    [RelayCommand]
    public void RefreshVideos()
    {
        Videos.Clear();
        if (string.IsNullOrWhiteSpace(_workspaceService.WorkspacePath)) return;

        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        if (!Directory.Exists(videosDir)) return;

        var files = Directory.GetFiles(videosDir, "*.mp4")
            .Concat(Directory.GetFiles(videosDir, "*.mp3"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach (var fi in files)
        {
            Videos.Add(new VideoFileInfo(fi.Name, fi.FullName, fi.Length, fi.CreationTime));
        }

        SelectedVideo = Videos.FirstOrDefault();
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(HasNoVideos));
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

    public Func<string, bool>? ConfirmDeleteHandler { get; set; }

    [RelayCommand]
    public void DeleteVideo(VideoFileInfo? video)
    {
        var target = video ?? SelectedVideo;
        if (target == null || !File.Exists(target.FullPath)) return;

        bool isConfirmed = false;
        if (ConfirmDeleteHandler != null)
        {
            isConfirmed = ConfirmDeleteHandler(target.FileName);
        }
        else
        {
            var confirm = System.Windows.MessageBox.Show(
                $"هل أنت متأكد من رغبتك في حذف الملف نهائياً؟\n{target.FileName}",
                "تأكيد الحذف",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            isConfirmed = (confirm == System.Windows.MessageBoxResult.Yes);
        }

        if (!isConfirmed) return;

        try
        {
            File.Delete(target.FullPath);
            StatusMessage = $"تم حذف الملف بنجاح: {target.FileName}";
            RefreshVideos();
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر حذف الملف: {ex.Message}";
            LoggingService.Instance.LogError($"Impossible de supprimer {target.FullPath}", ex);
        }
    }

    [RelayCommand]
    public void StartRenameVideo(VideoFileInfo? video)
    {
        var target = video ?? SelectedVideo;
        if (target == null || !File.Exists(target.FullPath)) return;

        VideoToRename = target;
        NewVideoName = Path.GetFileNameWithoutExtension(target.FileName);
        IsRenaming = true;
    }

    [RelayCommand]
    public void CancelRename()
    {
        IsRenaming = false;
        VideoToRename = null;
        NewVideoName = string.Empty;
    }

    [RelayCommand]
    public void ConfirmRename()
    {
        if (VideoToRename == null || !File.Exists(VideoToRename.FullPath) || string.IsNullOrWhiteSpace(NewVideoName))
        {
            CancelRename();
            return;
        }

        var cleanName = string.Concat(NewVideoName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            StatusMessage = "اسم الملف غير صالح.";
            return;
        }

        var ext = Path.GetExtension(VideoToRename.FullPath);
        var dir = Path.GetDirectoryName(VideoToRename.FullPath) ?? "";
        var newFullPath = Path.Combine(dir, cleanName + ext);

        if (string.Equals(VideoToRename.FullPath, newFullPath, StringComparison.OrdinalIgnoreCase))
        {
            CancelRename();
            return;
        }

        if (File.Exists(newFullPath))
        {
            StatusMessage = "يوجد ملف آخر يحمل نفس الاسم بالفعل.";
            return;
        }

        try
        {
            File.Move(VideoToRename.FullPath, newFullPath);
            StatusMessage = $"تم تغيير اسم الملف إلى: {cleanName + ext}";
            CancelRename();
            RefreshVideos();

            SelectedVideo = Videos.FirstOrDefault(v => v.FullPath == newFullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر إعادة التسمية: {ex.Message}";
            LoggingService.Instance.LogError($"Impossible de renommer {VideoToRename.FullPath} vers {newFullPath}", ex);
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
