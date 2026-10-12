using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.Views;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        using var workspace = new WorkspaceService();
        if (!workspace.InitializeWorkspace(Path.Combine(root, "workspace")).Success) throw new Exception("workspace");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                var catalog = new RecitationCatalogService(workspace);
                await catalog.InitializeAsync();
                var window = new AudioSourcesWindow(workspace);
                var method = typeof(AudioSourcesWindow).GetMethod("RefreshChoicesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
                await (Task)method.Invoke(window, [await catalog.GetDefaultAsync()])!;
                var grid = (DataGrid)window.FindName("LinksGrid");
                if (grid.Items.Count != 114) throw new Exception($"Expected 114 rows; got {grid.Items.Count}");
                var selector = (ComboBox)window.FindName("Reciters");
                Console.WriteLine($"Rows={grid.Items.Count}; Selected={selector.SelectedItem}; RTL={window.FlowDirection}");
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(1100, 700)); content.Arrange(new Rect(0, 0, 1100, 700)); content.UpdateLayout();
                await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                content.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1100, 700, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(root, "audio-sources.png"))) encoder.Save(file);
                if (args.Contains("--download"))
                {
                    using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                    var result = await new RecitationLibraryService(workspace).DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 1, cancellationToken: ct.Token);
                    var message = $"Real download: success={result.Success}; duration={result.Source?.DurationSeconds}; bytes={result.Source?.FileSizeBytes}; error={result.ErrorMessage}";
                    Console.WriteLine(message); await File.WriteAllTextAsync(Path.Combine(root, "download-result.txt"), message);
                }
                app.Shutdown(0);
            }
            catch (Exception ex) { Console.WriteLine(ex); app.Shutdown(1); }
        };
        app.Run();
    }
}
