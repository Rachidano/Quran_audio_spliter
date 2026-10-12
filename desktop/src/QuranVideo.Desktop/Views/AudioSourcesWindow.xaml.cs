using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.Views;

public partial class AudioSourcesWindow : Window
{
    private readonly WorkspaceService _workspace;
    private readonly RecitationCatalogService _catalog;
    private readonly RecitationLibraryService _library;
    private readonly ObservableCollection<AudioLinkRow> _rows = new();
    private RecitationChoice? _current;
    private Dictionary<int, string> _saved = new();
    private string _savedName = "";
    private bool _savedDefault;
    private bool _switching, _busy, _canClose;
    private CancellationTokenSource? _download;

    public AudioSourcesWindow(WorkspaceService workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        _catalog = new(workspace);
        _library = new(workspace);
        LinksGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await RunAsync(async () =>
        {
            await _catalog.InitializeAsync();
            await RefreshChoicesAsync(await _catalog.GetDefaultAsync());
        });
        Closing += OnClosing;
    }

    private async Task RefreshChoicesAsync(string selectedId)
    {
        _switching = true;
        try
        {
            var choices = await _catalog.GetCollectionsAsync();
            Reciters.ItemsSource = choices;
            var selected = choices.First(c => c.CollectionId == selectedId);
            Reciters.SelectedItem = selected;
            await LoadAsync(selected);
        }
        finally { _switching = false; }
    }

    private async Task LoadAsync(RecitationChoice choice)
    {
        var links = await _catalog.GetLinksAsync(choice.CollectionId);
        using var db = new AppDbContext(_workspace.GetDatabasePath());
        var downloaded = await db.AudioSources.Where(s => s.CollectionId == choice.CollectionId).ToListAsync();
        _current = choice;
        ReciterName.Text = choice.Name;
        DefaultChoice.IsChecked = await _catalog.GetDefaultAsync() == choice.CollectionId;
        _rows.Clear();
        foreach (var s in SurahCatalogService.GetAllSurahs())
        {
            var source = downloaded.FirstOrDefault(a => a.SurahNumber == s.Number);
            var exists = source != null && File.Exists(_workspace.ToAbsolutePath(source.RelativeFilePath));
            _rows.Add(new AudioLinkRow { Number = s.Number, Name = s.NameArabic, Url = links.GetValueOrDefault(s.Number, ""), Status = exists ? "تم التنزيل" : "لم يتم التنزيل" });
        }
        Remember();
        StatusText.Text = "114 سورة — عدّل الروابط ثم احفظ، أو اضغط تنزيل بجانب السورة المطلوبة.";
    }

    private void Remember()
    {
        _saved = _rows.ToDictionary(r => r.Number, r => r.Url);
        _savedName = ReciterName.Text;
        _savedDefault = DefaultChoice.IsChecked == true;
    }
    private bool HasChanges => _current != null && (_savedName != ReciterName.Text
        || _savedDefault != (DefaultChoice.IsChecked == true) || _rows.Any(r => _saved.GetValueOrDefault(r.Number) != r.Url));

    private async Task SaveAsync()
    {
        if (_current == null) return;
        if (string.IsNullOrWhiteSpace(ReciterName.Text)) throw new ArgumentException("أدخل اسم القارئ.");
        await _catalog.SaveLinksAsync(_current.CollectionId, _rows.ToDictionary(r => r.Number, r => r.Url));
        await _catalog.RenameAsync(_current.CollectionId, ReciterName.Text);
        if (DefaultChoice.IsChecked == true) await _catalog.SetDefaultAsync(_current.CollectionId);
        else if (await _catalog.GetDefaultAsync() == _current.CollectionId)
            DefaultChoice.IsChecked = true; // A default always exists; choose another to change it.
        Remember();
    }

    private async void OnReciterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switching || _busy || Reciters.SelectedItem is not RecitationChoice next) return;
        var previous = _current;
        await RunAsync(async () =>
        {
            try { if (HasChanges) await SaveAsync(); await LoadAsync(next); }
            catch
            {
                _switching = true; Reciters.SelectedItem = previous; _switching = false;
                throw;
            }
        });
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        await SaveAsync();
        await RefreshChoicesAsync(_current!.CollectionId);
        StatusText.Text = "تم حفظ اسم القارئ وروابط السور.";
    });

    private async void OnAddReciter(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (HasChanges) await SaveAsync();
        var added = await _catalog.AddReciterAsync(NewName.Text);
        NewName.Clear();
        await RefreshChoicesAsync(added.CollectionId);
    });

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioLinkRow row } || _current == null) return;
        await RunAsync(async () =>
        {
            await SaveAsync(); // The edited URL is the URL downloaded, not an old inventory entry.
            _download = new CancellationTokenSource(); CancelDownload.IsEnabled = true;
            try
            {
                row.Status = "جارٍ التنزيل";
                StatusText.Text = $"تنزيل سورة {row.Name} — {_current.Name}";
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress.IsIndeterminate = p < 0;
                    if (p >= 0) DownloadProgress.Value = p;
                    row.Status = p < 0 ? "جارٍ التنزيل" : $"{p:F0}%";
                });
                var result = await _library.DownloadSurahAudioAsync(_current.CollectionId, row.Number, progress, _download.Token);
                row.Status = result.Success ? "تم التنزيل" : "لم يكتمل التنزيل";
                StatusText.Text = result.Success ? $"تم تنزيل سورة {row.Name}. يمكنك فتحها في إعداد الفيديو." : result.ErrorMessage;
            }
            finally
            {
                _download.Dispose(); _download = null; CancelDownload.IsEnabled = false;
                DownloadProgress.IsIndeterminate = false;
            }
        });
    }

    private void OnCancel(object sender, RoutedEventArgs e) => _download?.Cancel();

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; HeaderForm.IsEnabled = false; LinksGrid.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { _busy = false; HeaderForm.IsEnabled = true; LinksGrid.IsEnabled = true; }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_canClose) return;
        if (_busy) { e.Cancel = true; StatusText.Text = "انتظر اكتمال العملية أو ألغِ التنزيل قبل الإغلاق."; return; }
        if (!HasChanges) return;
        e.Cancel = true;
        var answer = MessageBox.Show(this, "هل تريد حفظ التعديلات قبل الإغلاق؟", "حفظ الروابط", MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question, MessageBoxResult.Yes, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (answer == MessageBoxResult.Cancel) return;
        if (answer == MessageBoxResult.Yes)
        {
            var saved = false;
            await RunAsync(async () => { await SaveAsync(); saved = true; });
            if (!saved) return;
        }
        _canClose = true; Close();
    }
}

public sealed partial class AudioLinkRow : ObservableObject
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _status = "";

    public bool IsDownloaded => Status == "تم التنزيل";
    public bool IsDownloading => Status.StartsWith("جارٍ") || Status.EndsWith("%");

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(IsDownloaded));
        OnPropertyChanged(nameof(IsDownloading));
    }
}
