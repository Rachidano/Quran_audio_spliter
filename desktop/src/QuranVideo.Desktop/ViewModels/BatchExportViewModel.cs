using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class BatchExportViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;
    private readonly BatchExportService _batchService;

    [ObservableProperty]
    private ObservableCollection<SurahInfo> _surahs = new();

    [ObservableProperty]
    private SurahInfo? _selectedSurah;

    [ObservableProperty]
    private int _startAyah = 1;

    [ObservableProperty]
    private int _endAyah = 7;

    [ObservableProperty]
    private bool _isHorizontal = true;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "جاهز لجدولة التصدير بالدفعة.";

    [ObservableProperty]
    private ObservableCollection<BatchExportJobEntity> _jobs = new();

    public bool HasJobs => Jobs.Count > 0;
    public bool HasNoJobs => Jobs.Count == 0;

    partial void OnJobsChanged(ObservableCollection<BatchExportJobEntity> value)
    {
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(HasNoJobs));
    }

    public BatchExportViewModel(WorkspaceService workspaceService, BatchExportService? batchService = null)
    {
        _workspaceService = workspaceService;
        _batchService = batchService ?? new BatchExportService(workspaceService);

        _batchService.JobProgressChanged += OnJobProgressChanged;
        _batchService.JobStatusChanged += OnJobStatusChanged;
        _batchService.QueueFinished += OnQueueFinished;

        LoadSurahs();
        _ = LoadJobsAsync();
    }

    private void LoadSurahs()
    {
        var list = SurahCatalogService.GetAllSurahs();
        Surahs = new ObservableCollection<SurahInfo>(list);
        SelectedSurah = Surahs.FirstOrDefault();
        if (SelectedSurah != null)
        {
            StartAyah = 1;
            EndAyah = SelectedSurah.AyahCount;
        }
    }

    partial void OnSelectedSurahChanged(SurahInfo? value)
    {
        if (value != null)
        {
            StartAyah = 1;
            EndAyah = value.AyahCount;
        }
    }

    public async Task LoadJobsAsync()
    {
        await _batchService.RecoverInterruptedJobsAsync();
        var list = await _batchService.GetJobsAsync();
        Jobs = new ObservableCollection<BatchExportJobEntity>(list);
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(HasNoJobs));
    }

    [RelayCommand]
    private async Task EnqueueRangeAsync()
    {
        if (SelectedSurah == null) return;

        var orientation = IsHorizontal ? VideoOrientation.Horizontal : VideoOrientation.Vertical;
        StatusMessage = "جارٍ فحص الآيات وتجميد اللقطات (Snapshot)...";

        var catalog = new RecitationCatalogService(_workspaceService);
        await catalog.InitializeAsync();
        var defaultId = await catalog.GetDefaultAsync();
        var reciter = (await catalog.GetCollectionsAsync()).Single(r => r.CollectionId == defaultId);
        var result = await _batchService.EnqueueRangeAsync(
            SelectedSurah.Number,
            StartAyah,
            EndAyah,
            orientation, reciter.ReciterId, reciter.CollectionId);

        await LoadJobsAsync();

        if (result.EnqueuedCount == 0 && result.SkippedCount > 0)
        {
            var alreadyCount = result.SkippedDetails.Count(d => d.Contains("موجودة بالفعل"));
            if (alreadyCount == result.SkippedCount)
            {
                StatusMessage = $"لم تتم إضافة أي عناصر: جميع الآيات المحددة ({alreadyCount}) موجودة بالفعل في قائمة الانتظار.";
            }
            else
            {
                StatusMessage = $"لم تتم جدولة أي آية: {result.SkippedCount} آية تم تخطيها (إما غير مسجلة صوتياً أو مضافة بالفعل).";
            }
        }
        else if (result.SkippedCount > 0)
        {
            StatusMessage = $"تمت إضافة {result.EnqueuedCount} آية إلى قائمة الانتظار (تم تخطي {result.SkippedCount} آية: مكررة أو غير مسجلة).";
        }
        else
        {
            StatusMessage = $"تمت إضافة {result.EnqueuedCount} آية إلى قائمة الانتظار بنجاح.";
        }
    }

    [RelayCommand]
    private async Task StartQueueAsync()
    {
        if (IsProcessing) return;

        IsProcessing = true;
        StatusMessage = "جارٍ تصدير الفيديوهات بالتتابع (مشفر واحد لضمان استقرار النظام)...";

        try
        {
            await _batchService.ProcessQueueAsync();
            StatusMessage = "اكتملت معالجة جميع المهام المجدولة.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في المعالجة: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            await LoadJobsAsync();
        }
    }

    [RelayCommand]
    private void StopQueue()
    {
        _batchService.CancelQueue();
        StatusMessage = "جارٍ إيقاف المعالجة...";
    }

    [RelayCommand]
    private async Task CancelJobAsync(BatchExportJobEntity? job)
    {
        if (job == null) return;
        await _batchService.CancelJobAsync(job.Id);
        await LoadJobsAsync();
        StatusMessage = $"تم إلغاء المهمة #{job.Id} للآية {job.AyahId}.";
    }

    [RelayCommand]
    private async Task DeleteJobAsync(BatchExportJobEntity? job)
    {
        if (job == null) return;
        await _batchService.DeleteJobAsync(job.Id);
        await LoadJobsAsync();
        StatusMessage = $"تم حذف المهمة #{job.Id} للآية {job.AyahId} نهائياً من القائمة 🗑️";
    }

    [RelayCommand]
    private async Task ClearFinishedJobsAsync()
    {
        var count = await _batchService.ClearFinishedJobsAsync();
        await LoadJobsAsync();
        StatusMessage = $"تم تنظيف {count} من المهام المكتملة/الملغاة 🧹";
    }

    [RelayCommand]
    private async Task ClearAllJobsAsync()
    {
        var count = await _batchService.ClearAllPendingJobsAsync();
        await LoadJobsAsync();
        StatusMessage = $"تم إفراغ {count} من المهام من القائمة 🗑️";
    }

    [RelayCommand]
    private async Task RetryJobAsync(BatchExportJobEntity? job)
    {
        if (job == null) return;
        await _batchService.RetryJobAsync(job.Id);
        await LoadJobsAsync();
    }

    [RelayCommand]
    private void OpenVideosFolder()
    {
        var videosDir = Path.Combine(_workspaceService.WorkspacePath, "media", "videos");
        Directory.CreateDirectory(videosDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = videosDir,
            UseShellExecute = true
        });
    }

    private void OnJobProgressChanged(BatchExportJobEntity updatedJob)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var match = Jobs.FirstOrDefault(j => j.Id == updatedJob.Id);
            if (match != null)
            {
                match.ProgressPercent = updatedJob.ProgressPercent;
            }
        });
    }

    private void OnJobStatusChanged(BatchExportJobEntity updatedJob)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var index = -1;
            for (int i = 0; i < Jobs.Count; i++)
            {
                if (Jobs[i].Id == updatedJob.Id) { index = i; break; }
            }
            if (index >= 0)
            {
                Jobs[index] = updatedJob;
            }
            else
            {
                Jobs.Insert(0, updatedJob);
            }
        });
    }

    private void OnQueueFinished()
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            IsProcessing = false;
        });
    }

    public static string FormatStatus(BatchJobStatus status) => status switch
    {
        BatchJobStatus.Pending => "في الانتظار ⏳",
        BatchJobStatus.Running => "جارٍ التصدير ⚙",
        BatchJobStatus.Completed => "مكتمل ✔",
        BatchJobStatus.Failed => "فشل ✖",
        BatchJobStatus.Cancelled => "ملغى ⏹",
        BatchJobStatus.Interrupted => "متوقف / مقطوع ⚠️",
        _ => "غير محدد"
    };
}
