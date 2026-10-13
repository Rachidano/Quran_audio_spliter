using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuranVideo.Desktop.ViewModels;

namespace QuranVideo.Desktop.Views;

public partial class MyVideosView : UserControl
{
    private DispatcherTimer? _positionTimer;
    private bool _isUserSeeking;

    public MyVideosView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MyVideosViewModel vm) return;

        vm.PlayRequested += OnPlayRequested;
        vm.PauseRequested += OnPauseRequested;
        vm.StopRequested += OnStopRequested;
        vm.SeekRequested += OnSeekRequested;
        vm.ClosePlayerRequested += OnClosePlayerRequested;
        vm.FullscreenRequested += OnFullscreenRequested;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        if (vm.CurrentMediaUri != null)
        {
            LoadMediaSource(vm.CurrentMediaUri, autoPlay: vm.IsPlaying);
        }

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTimerTick;
        _positionTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _positionTimer?.Stop();
        _positionTimer = null;

        if (DataContext is MyVideosViewModel vm)
        {
            if (vm.IsFullscreen)
            {
                vm.IsFullscreen = false;
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.WindowStyle = _previousWindowStyle;
                    window.WindowState = _previousWindowState;
                }
            }

            vm.PlayRequested -= OnPlayRequested;
            vm.PauseRequested -= OnPauseRequested;
            vm.StopRequested -= OnStopRequested;
            vm.SeekRequested -= OnSeekRequested;
            vm.ClosePlayerRequested -= OnClosePlayerRequested;
            vm.FullscreenRequested -= OnFullscreenRequested;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        CloseMedia();
    }

    private void OnFullscreenRequested(bool isFullscreen)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        if (isFullscreen)
        {
            _previousWindowState = window.WindowState;
            _previousWindowStyle = window.WindowStyle;
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.WindowStyle = _previousWindowStyle;
            window.WindowState = _previousWindowState;
        }
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is MyVideosViewModel vm && vm.IsPlaying && !_isUserSeeking)
        {
            if (PlayerMediaElement.NaturalDuration.HasTimeSpan)
            {
                vm.PlaybackPositionSec = PlayerMediaElement.Position.TotalSeconds;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyVideosViewModel.CurrentMediaUri) && DataContext is MyVideosViewModel vm)
        {
            if (vm.CurrentMediaUri != null)
            {
                LoadMediaSource(vm.CurrentMediaUri, autoPlay: vm.IsPlaying);
            }
            else
            {
                CloseMedia();
            }
        }
    }

    private void LoadMediaSource(Uri uri, bool autoPlay)
    {
        try
        {
            if (PlayerMediaElement.Source != uri)
            {
                PlayerMediaElement.Source = uri;
            }

            if (autoPlay)
            {
                PlayerMediaElement.Play();
            }
            else
            {
                PlayerMediaElement.Stop();
            }
        }
        catch
        {
            // Ignore format/preview exceptions gracefully
        }
    }

    private void OnPlayRequested()
    {
        if (DataContext is MyVideosViewModel vm && vm.CurrentMediaUri != null)
        {
            if (PlayerMediaElement.Source != vm.CurrentMediaUri)
            {
                PlayerMediaElement.Source = vm.CurrentMediaUri;
            }
            PlayerMediaElement.Play();
        }
    }

    private void OnPauseRequested()
    {
        PlayerMediaElement.Pause();
    }

    private void OnStopRequested()
    {
        PlayerMediaElement.Stop();
        PlayerMediaElement.Position = TimeSpan.Zero;
    }

    private void OnSeekRequested(double posSec)
    {
        PlayerMediaElement.Position = TimeSpan.FromSeconds(Math.Max(0, posSec));
    }

    private void OnClosePlayerRequested()
    {
        CloseMedia();
    }

    private void CloseMedia()
    {
        try
        {
            PlayerMediaElement.Stop();
            PlayerMediaElement.Close();
            PlayerMediaElement.Source = null;
        }
        catch
        {
            // Ignore teardown issues
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is MyVideosViewModel vm)
        {
            if (PlayerMediaElement.NaturalDuration.HasTimeSpan)
            {
                vm.TotalDurationSec = PlayerMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            }
            vm.HasPlayerError = false;
        }
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MyVideosViewModel vm)
        {
            vm.IsPlaying = false;
            vm.PlaybackPositionSec = 0;
            PlayerMediaElement.Position = TimeSpan.Zero;
        }
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        if (DataContext is MyVideosViewModel vm)
        {
            vm.IsPlaying = false;
            vm.HasPlayerError = true;
            vm.PlayerStatusMessage = e.ErrorException?.Message ?? "تعذر فتح أو تشغيل هذا الملف.";
        }
    }

    private void OnScrubberValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && (slider.IsMouseCaptureWithin || slider.IsMouseOver))
        {
            _isUserSeeking = true;
            if (DataContext is MyVideosViewModel vm)
            {
                vm.Seek(e.NewValue);
            }
            _isUserSeeking = false;
        }
    }
}
