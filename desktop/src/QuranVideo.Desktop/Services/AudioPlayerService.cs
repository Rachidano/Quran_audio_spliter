using NAudio.Wave;

namespace QuranVideo.Desktop.Services;

public sealed class AudioPlayerService : IDisposable
{
    private readonly object _lock = new();
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _reader;
    private System.Threading.Timer? _positionTimer;

    private double _segmentStartSec;
    private double _segmentEndSec;
    private bool _isLoopingSegment;

    public event Action<double>? PositionChanged;
    public event Action? PlaybackStopped;

    public bool IsPlaying
    {
        get
        {
            lock (_lock)
            {
                return _waveOut?.PlaybackState == PlaybackState.Playing;
            }
        }
    }

    public double DurationSec
    {
        get
        {
            lock (_lock)
            {
                return _reader?.TotalTime.TotalSeconds ?? 0.0;
            }
        }
    }

    public double CurrentTimeSec
    {
        get
        {
            lock (_lock)
            {
                return _reader?.CurrentTime.TotalSeconds ?? 0.0;
            }
        }
        set => Seek(value);
    }

    public float Volume
    {
        get
        {
            lock (_lock)
            {
                return _waveOut?.Volume ?? 1.0f;
            }
        }
        set
        {
            lock (_lock)
            {
                if (_waveOut != null) _waveOut.Volume = Math.Clamp(value, 0.0f, 1.0f);
            }
        }
    }

    public void Load(string audioFilePath)
    {
        lock (_lock)
        {
            StopInternal();
            _reader?.Dispose();
            _waveOut?.Dispose();

            _reader = new AudioFileReader(audioFilePath);
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 150,
                NumberOfBuffers = 3
            };
            _waveOut.Init(_reader);
            _waveOut.PlaybackStopped += OnWaveOutPlaybackStopped;

            _positionTimer?.Dispose();
            _positionTimer = new System.Threading.Timer(OnPositionTimerTick, null, 0, 50); // 20 updates per second
        }
    }

    public void Play()
    {
        lock (_lock)
        {
            _isLoopingSegment = false;
            _waveOut?.Play();
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _waveOut?.Pause();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        _isLoopingSegment = false;
        _waveOut?.Stop();
        if (_reader != null)
        {
            _reader.CurrentTime = TimeSpan.Zero;
        }
    }

    public void Seek(double timeSec)
    {
        double targetSec;
        lock (_lock)
        {
            if (_reader == null) return;
            var max = DurationSec > 0 ? DurationSec : double.MaxValue;
            targetSec = Math.Max(0, timeSec);
            if (targetSec > max) targetSec = max;
            _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
        }
        PositionChanged?.Invoke(targetSec);
    }

    public void PlaySegment(double startTimeSec, double endTimeSec, bool loop = false)
    {
        lock (_lock)
        {
            if (_reader == null || _waveOut == null) return;

            _segmentStartSec = Math.Max(0, startTimeSec);
            _segmentEndSec = Math.Min(DurationSec, Math.Max(_segmentStartSec + 0.1, endTimeSec));
            _isLoopingSegment = loop;

            var targetSec = Math.Clamp(_segmentStartSec, 0, DurationSec);
            _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
            _waveOut.Play();
        }
        PositionChanged?.Invoke(_segmentStartSec);
    }

    public void StepForward(double deltaSec = 1.0)
    {
        Seek(CurrentTimeSec + deltaSec);
    }

    public void StepBackward(double deltaSec = 1.0)
    {
        Seek(CurrentTimeSec - deltaSec);
    }

    private void OnPositionTimerTick(object? state)
    {
        double cur;
        bool looping;
        double segEnd;
        double segStart;

        lock (_lock)
        {
            if (_reader == null || _waveOut == null || !IsPlaying) return;

            cur = _reader.CurrentTime.TotalSeconds;
            looping = _isLoopingSegment;
            segEnd = _segmentEndSec;
            segStart = _segmentStartSec;

            if (looping && cur >= segEnd)
            {
                var targetSec = Math.Clamp(segStart, 0, DurationSec);
                _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
                cur = targetSec;
            }
        }

        PositionChanged?.Invoke(cur);
    }

    private void OnWaveOutPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _positionTimer?.Dispose();
            _positionTimer = null;

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnWaveOutPlaybackStopped;
                _waveOut.Dispose();
                _waveOut = null;
            }

            _reader?.Dispose();
            _reader = null;
        }
    }
}
