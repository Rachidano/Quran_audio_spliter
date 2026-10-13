using System.IO;
using System.Windows;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using QuranVideo.Desktop.Views;

namespace QuranVideo.Desktop;

public partial class App : Application
{
    private WorkspaceService? _workspaceService;

    public App()
    {
        Resources.Add("BooleanToVisibilityConverter", new System.Windows.Controls.BooleanToVisibilityConverter());

        bool isShowingError = false;
        DispatcherUnhandledException += (s, e) =>
        {
            LoggingService.Instance.LogError("Exception non gérée (Dispatcher)", e.Exception);
            e.Handled = true;

            if (!isShowingError)
            {
                isShowingError = true;
                try
                {
                    MessageBox.Show(
                        $"حدث خطأ غير متوقع:\n{e.Exception.Message}",
                        "صانع فيديو القرآن الكريم — تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        MessageBoxResult.OK,
                        MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                }
                finally
                {
                    isShowingError = false;
                }
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LoggingService.Instance.LogError("Exception fatale (AppDomain)", ex);
            }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _workspaceService = new WorkspaceService();
        var (success, errorMsg) = _workspaceService.InitializeWorkspace();

        if (!success)
        {
            MessageBox.Show(
                errorMsg ?? "تعذر فتح مجلد العمل.",
                "صانع فيديو القرآن الكريم — تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                MessageBoxResult.OK,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            Shutdown(1);
            return;
        }

        var mainVm = new MainViewModel(_workspaceService);
        var mainWindow = new MainWindow(mainVm);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _workspaceService?.Dispose();
        base.OnExit(e);
    }
}
