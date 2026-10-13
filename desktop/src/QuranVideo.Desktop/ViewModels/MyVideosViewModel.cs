using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Data;
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
    public bool IsMp4 => FileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
    public bool IsMp3 => FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || FileName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase);
    public string FormatBadge => IsMp4 ? "MP4" : (IsMp3 ? "MP3" : "MEDIA");
    public string CleanTitle => Path.GetFileNameWithoutExtension(FileName).Replace('_', ' ');
}

public sealed partial class MyVideosViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;

    public event Action? ClosePlayerRequested;
    public event Action? PlayRequested;
    public event Action? PauseRequested;
    public event Action? StopRequested;
    public event Action<double>? SeekRequested;

    [ObservableProperty]
    private VideoFileInfo? _selectedVideo;

    partial void OnSelectedVideoChanged(VideoFileInfo? value)
    {
        ClosePlayerRequested?.Invoke();
        IsPlaying = false;
        PlaybackPositionSec = 0;
        TotalDurationSec = 0;
        HasPlayerError = false;
        PlayerStatusMessage = string.Empty;

        if (value != null && File.Exists(value.FullPath))
        {
            IsPlayerActive = true;
            CurrentMediaUri = new Uri(value.FullPath);
        }
        else
        {
            IsPlayerActive = false;
            CurrentMediaUri = null;
            if (value != null && !File.Exists(value.FullPath))
            {
                HasPlayerError = true;
                PlayerStatusMessage = "الملف المحدد غير موجود على القرص.";
            }
        }
    }

    [ObservableProperty]
    private Uri? _currentMediaUri;

    [ObservableProperty]
    private bool _isPlayerActive;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _playbackPositionSec;

    [ObservableProperty]
    private double _totalDurationSec;

    [ObservableProperty]
    private double _volume = 0.85;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _hasPlayerError;

    [ObservableProperty]
    private string _playerStatusMessage = string.Empty;

    public string FormattedPlaybackPosition => FormatTime(PlaybackPositionSec);
    public string FormattedTotalDuration => FormatTime(TotalDurationSec);

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    partial void OnPlaybackPositionSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedPlaybackPosition));
    }

    partial void OnTotalDurationSecChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedTotalDuration));
    }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _mediaTypeFilter = "All"; // "All", "Video", "Audio"

    public bool IsFilterAll => MediaTypeFilter == "All";
    public bool IsFilterVideo => MediaTypeFilter == "Video";
    public bool IsFilterAudio => MediaTypeFilter == "Audio";

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnMediaTypeFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterVideo));
        OnPropertyChanged(nameof(IsFilterAudio));
        ApplyFilter();
    }

    [RelayCommand]
    public void SetFilterAll() => MediaTypeFilter = "All";

    [RelayCommand]
    public void SetFilterVideo() => MediaTypeFilter = "Video";

    [RelayCommand]
    public void SetFilterAudio() => MediaTypeFilter = "Audio";

    public ObservableCollection<VideoFileInfo> Videos { get; } = new();
    public ObservableCollection<VideoFileInfo> FilteredVideos { get; } = new();

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

    private void ApplyFilter()
    {
        FilteredVideos.Clear();
        var q = SearchQuery.Trim();
        foreach (var v in Videos)
        {
            if (MediaTypeFilter == "Video" && !v.IsMp4) continue;
            if (MediaTypeFilter == "Audio" && !v.IsMp3) continue;
            if (!string.IsNullOrWhiteSpace(q) && 
                !v.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) && 
                !v.CleanTitle.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;

            FilteredVideos.Add(v);
        }
    }

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

        ApplyFilter();

        if (SelectedVideo == null || !Videos.Any(v => v.FullPath == SelectedVideo.FullPath))
        {
            SelectedVideo = FilteredVideos.FirstOrDefault() ?? Videos.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(HasNoVideos));
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (SelectedVideo == null || !File.Exists(SelectedVideo.FullPath)) return;

        if (IsPlaying)
        {
            IsPlaying = false;
            PauseRequested?.Invoke();
        }
        else
        {
            IsPlaying = true;
            PlayRequested?.Invoke();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        IsPlaying = false;
        PlaybackPositionSec = 0;
        StopRequested?.Invoke();
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    public event Action<bool>? FullscreenRequested;

    [RelayCommand]
    public void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        FullscreenRequested?.Invoke(IsFullscreen);
    }

    [RelayCommand]
    public void Seek(double posSec)
    {
        PlaybackPositionSec = posSec;
        SeekRequested?.Invoke(posSec);
    }

    [RelayCommand]
    public void ClosePlayer()
    {
        ClosePlayerRequested?.Invoke();
        IsPlayerActive = false;
        IsPlaying = false;
        CurrentMediaUri = null;
        PlaybackPositionSec = 0;
        TotalDurationSec = 0;
    }

    [RelayCommand]
    public void PlaySelectedVideo()
    {
        if (SelectedVideo != null && File.Exists(SelectedVideo.FullPath))
        {
            IsPlayerActive = true;
            CurrentMediaUri = new Uri(SelectedVideo.FullPath);
            IsPlaying = true;
            PlayRequested?.Invoke();
        }
    }

    [RelayCommand]
    public void PlayInExternalPlayer()
    {
        if (SelectedVideo != null && File.Exists(SelectedVideo.FullPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(SelectedVideo.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Impossible de lire la vidéo en externe : {SelectedVideo.FullPath}", ex);
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

        ClosePlayer();

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

        ClosePlayer();

        try
        {
            var oldRelPath = Path.GetRelativePath(_workspaceService.WorkspacePath, VideoToRename.FullPath);
            var newRelPath = Path.GetRelativePath(_workspaceService.WorkspacePath, newFullPath);

            File.Move(VideoToRename.FullPath, newFullPath);

            // Update database records pointing to this video file
            try
            {
                var dbPath = _workspaceService.GetDatabasePath();
                using var db = new AppDbContext(dbPath);
                var matched = db.Projects
                    .Where(p => p.OutputVideoPath == oldRelPath || p.OutputVideoPath == VideoToRename.FullPath)
                    .ToList();
                foreach (var p in matched)
                {
                    p.OutputVideoPath = newRelPath;
                }
                if (matched.Count > 0)
                {
                    db.SaveChanges();
                }
            }
            catch (Exception dbEx)
            {
                LoggingService.Instance.LogError("Erreur mise a jour chemin projet en base apres renommage", dbEx);
            }

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
