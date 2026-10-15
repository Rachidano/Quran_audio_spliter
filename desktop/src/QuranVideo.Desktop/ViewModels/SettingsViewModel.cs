using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;
    private readonly BackupService _backupService;
    private readonly Action _onClose;

    public string AppVersion => LoggingService.AppVersion;
    public string WorkspacePath => _workspaceService.WorkspacePath;

    [ObservableProperty]
    private string _diagnosticStatusMessage = string.Empty;

    [ObservableProperty]
    private string _backupStatusMessage = string.Empty;

    [ObservableProperty]
    private string _packStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public SettingsViewModel(WorkspaceService workspaceService, Action onClose, BackupService? backupService = null)
    {
        _workspaceService = workspaceService;
        _backupService = backupService ?? new BackupService(workspaceService);
        _onClose = onClose;


    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (IsBusy) return;

        var saveDialog = new SaveFileDialog
        {
            Title = "حفظ النسخة الاحتياطية لمشروع القرآن الكريم",
            Filter = "أرشيف مضغوط (*.zip)|*.zip",
            FileName = $"quran_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
        };

        if (saveDialog.ShowDialog() != true) return;

        IsBusy = true;
        BackupStatusMessage = "جارٍ تجهيز النسخة الاحتياطية ولقطة قاعدة البيانات...";

        try
        {
            var progress = new Progress<int>(p => BackupStatusMessage = $"جارٍ إنشاء النسخة الاحتياطية ({p}%)...");
            var res = await Task.Run(() => _backupService.CreateBackupAsync(saveDialog.FileName, progress));

            if (res.Success)
            {
                var mb = res.TotalBytes / (1024.0 * 1024.0);
                BackupStatusMessage = $"تم حفظ النسخة الاحتياطية بنجاح ({res.FileCount} ملفات — {mb:F1} ميغابايت) في :\n{Path.GetFileName(res.BackupFilePath)}";
            }
            else
            {
                BackupStatusMessage = $"فشل إنشاء النسخة الاحتياطية : {res.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"خطأ : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsBusy) return;

        var openDialog = new OpenFileDialog
        {
            Title = "اختر ملف النسخة الاحتياطية للاسترجاع",
            Filter = "أرشيف مضغوط (*.zip)|*.zip"
        };

        if (openDialog.ShowDialog() != true) return;

        IsBusy = true;
        BackupStatusMessage = "جارٍ استرجاع المشروع من الأرشيف...";

        try
        {
            var progress = new Progress<int>(p => BackupStatusMessage = $"جارٍ استرجاع الملفات ({p}%)...");
            var res = await Task.Run(() => _backupService.RestoreBackupAsync(openDialog.FileName, _workspaceService.WorkspacePath, progress));

            if (res.Success)
            {
                BackupStatusMessage = $"تم استرجاع المشروع بنجاح ({res.RestoredFileCount} ملفات مستعادة). يُرجى إعادة تشغيل التطبيق لتحديث كافة الجلسات.";
            }
            else
            {
                BackupStatusMessage = $"فشل الاسترجاع : {res.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"خطأ أثناء الاسترجاع : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var targetFile = Path.Combine(desktopDir, $"quran_video_diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            await LoggingService.Instance.ExportDiagnosticsAsync(_workspaceService.WorkspacePath, targetFile);
            DiagnosticStatusMessage = $"تم تصدير التقرير بنجاح إلى سطح المكتب :\n{Path.GetFileName(targetFile)}";
        }
        catch (Exception ex)
        {
            DiagnosticStatusMessage = $"تعذر تصدير التقرير : {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportPackAsync()
    {
        if (IsBusy) return;

        var openDialog = new OpenFileDialog
        {
            Title = "اختر ملف بيان حزمة المصحف (manifest.json)",
            Filter = "ملف البيان (manifest.json)|manifest.json|كافة الملفات (*.*)|*.*",
            FileName = "manifest.json"
        };

        if (openDialog.ShowDialog() != true) return;

        var packDir = Path.GetDirectoryName(openDialog.FileName);
        if (string.IsNullOrEmpty(packDir)) return;

        IsBusy = true;
        PackStatusMessage = "جارٍ استيراد حزمة المصحف والتحقق من الصفحات والإحداثيات...";

        try
        {
            var progress = new Progress<ImportProgressReport>(p =>
            {
                PackStatusMessage = $"جارٍ استيراد الصفحة {p.CurrentPage} من {p.TotalPages}...";
            });

            var importer = new CorpusImportService(_workspaceService);
            var (ok, err) = await importer.ImportPackAsync(packDir, progress: progress);
            if (ok)
            {
                PackStatusMessage = "تم استيراد حزمة المصحف بنجاح ✔";
            }
            else
            {
                PackStatusMessage = $"فشل استيراد الحزمة: {err}";
            }
        }
        catch (Exception ex)
        {
            PackStatusMessage = $"خطأ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenAudioSources()
    {
        var window = new QuranVideo.Desktop.Views.AudioSourcesWindow(_workspaceService)
        { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
    }

    [RelayCommand]
    private void Close()
    {
        _onClose();
    }
}
