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
    private bool _isSegmentPlay;
    private double _playbackBaseTimeSec;
    private long _playbackStartBytePosition;

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
                if (_reader == null) return 0.0;
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    long currentBytes = _waveOut.GetPosition();
                    long bytesDelta = Math.Max(0, currentBytes - _playbackStartBytePosition);
                    double bytesPerSec = _reader.WaveFormat.AverageBytesPerSecond;
                    if (bytesPerSec > 0)
                    {
                        double elapsed = bytesDelta / bytesPerSec;
                        return Math.Min(DurationSec, Math.Max(0, _playbackBaseTimeSec + elapsed));
                    }
                }
                return _playbackBaseTimeSec;
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

            _playbackBaseTimeSec = 0.0;
            _playbackStartBytePosition = 0;

            _reader = new AudioFileReader(audioFilePath);
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100,
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
            _isSegmentPlay = false;
            if (_reader != null)
            {
                _reader.CurrentTime = TimeSpan.FromSeconds(_playbackBaseTimeSec);
                _playbackStartBytePosition = _waveOut?.GetPosition() ?? 0;
            }
            _waveOut?.Play();
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _playbackBaseTimeSec = CurrentTimeSec;
            _waveOut?.Pause();
            if (_reader != null)
            {
                _reader.CurrentTime = TimeSpan.FromSeconds(_playbackBaseTimeSec);
            }
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
        _isSegmentPlay = false;
        _waveOut?.Stop();
        _playbackBaseTimeSec = 0.0;
        _playbackStartBytePosition = 0;
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
            _playbackBaseTimeSec = targetSec;
            _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
            _playbackStartBytePosition = _waveOut?.GetPosition() ?? 0;
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
            _isSegmentPlay = true;

            var targetSec = Math.Clamp(_segmentStartSec, 0, DurationSec);
            _playbackBaseTimeSec = targetSec;
            _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
            _playbackStartBytePosition = _waveOut.GetPosition();
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
        bool shouldStop = false;

        lock (_lock)
        {
            if (_reader == null || _waveOut == null || !IsPlaying) return;

            cur = CurrentTimeSec;
            looping = _isLoopingSegment;
            segEnd = _segmentEndSec;
            segStart = _segmentStartSec;

            if (looping && cur >= segEnd)
            {
                var targetSec = Math.Clamp(segStart, 0, DurationSec);
                _playbackBaseTimeSec = targetSec;
                _reader.CurrentTime = TimeSpan.FromSeconds(targetSec);
                _playbackStartBytePosition = _waveOut.GetPosition();
                cur = targetSec;
            }
            else if (!looping && _isSegmentPlay && cur >= segEnd)
            {
                _isSegmentPlay = false;
                _waveOut.Pause();
                _playbackBaseTimeSec = segEnd;
                _reader.CurrentTime = TimeSpan.FromSeconds(segEnd);
                cur = segEnd;
                shouldStop = true;
            }
        }

        PositionChanged?.Invoke(cur);
        if (shouldStop)
        {
            PlaybackStopped?.Invoke();
        }
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
