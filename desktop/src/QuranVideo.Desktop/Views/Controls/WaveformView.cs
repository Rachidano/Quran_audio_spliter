using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QuranVideo.Desktop.Views.Controls;

/// <summary>
/// Onde sonore du studio : amplitude réelle, segment de l'ayah surligné avec ses marqueurs de début
/// et de fin, tête de lecture et échelle de temps. Un clic ou un glissement positionne la lecture.
/// Jusqu'à <see cref="FullViewMaxSec"/> secondes, tout le fichier est affiché ; au-delà, la vue est
/// centrée sur le segment et suit la tête de lecture page par page.
/// Les marqueurs de début et de fin se déplacent à la souris (liaison bidirectionnelle).
/// Par défaut, le temps va de droite à gauche (00:00 à droite), dans le sens de lecture de l'arabe
/// (<see cref="TimeRightToLeft"/>). Le sens est calculé par le contrôle lui-même (FlowDirection forcé
/// à gauche-droite) pour que les chiffres de l'échelle ne soient pas dessinés en miroir.
/// </summary>
public sealed class WaveformView : FrameworkElement
{
    public const double FullViewMaxSec = 120;
    public const double MinWindowSec = 20;
    private const double AxisHeight = 20;
    private const double ColumnStep = 2;
    /// <summary>Distance (px) à laquelle un clic attrape un marqueur au lieu de positionner la lecture.</summary>
    public const double MarkerGrabPx = 7;
    /// <summary>Écart minimal entre début et fin lors d'un glissement de marqueur.</summary>
    public const double MinSegmentSec = 0.05;

    private static readonly Brush BackgroundBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff)));
    private static readonly Brush OutsideWaveBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xcb, 0xd5, 0xe1)));
    private static readonly Brush SegmentWaveBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x0d, 0x94, 0x88)));
    private static readonly Brush SegmentFillBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0x34, 0xd3, 0x99)));
    private static readonly Brush MarkerBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3d)));
    private static readonly Brush AxisTextBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)));
    private static readonly Pen AxisPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0))), 1));
    private static readonly Pen MarkerPen = Frozen(new Pen(MarkerBrush, 2));
    private static readonly Pen PlayheadPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xdc, 0x26, 0x26))), 1.5));
    private static readonly Typeface AxisTypeface = new("Segoe UI");

    public static readonly DependencyProperty PeaksProperty = Register(nameof(Peaks), typeof(float[]), null, OnPeaksChanged);
    public static readonly DependencyProperty DurationSecProperty = Register(nameof(DurationSec), typeof(double), 0d, OnSegmentChanged);
    public static readonly DependencyProperty PositionSecProperty = Register(nameof(PositionSec), typeof(double), 0d, OnPositionChanged);
    public static readonly DependencyProperty SegmentStartSecProperty = Register(nameof(SegmentStartSec), typeof(double), 0d, OnSegmentChanged, twoWay: true);
    public static readonly DependencyProperty SegmentEndSecProperty = Register(nameof(SegmentEndSec), typeof(double), 0d, OnSegmentChanged, twoWay: true);

    public static readonly DependencyProperty TimeRightToLeftProperty = Register(nameof(TimeRightToLeft), typeof(bool), true, OnSegmentChanged);

    public static readonly DependencyProperty SeekCommandProperty = DependencyProperty.Register(
        nameof(SeekCommand), typeof(ICommand), typeof(WaveformView));

    private float _peakMax = 1f;
    private (double Start, double End) _window = (0, 1);
    private bool _isScrubbing;
    private enum DragTarget { None, Start, End }
    private DragTarget _dragTarget = DragTarget.None;

    public WaveformView()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Cursor = Cursors.Hand;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public float[]? Peaks { get => (float[]?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double DurationSec { get => (double)GetValue(DurationSecProperty); set => SetValue(DurationSecProperty, value); }
    public double PositionSec { get => (double)GetValue(PositionSecProperty); set => SetValue(PositionSecProperty, value); }
    public double SegmentStartSec { get => (double)GetValue(SegmentStartSecProperty); set => SetValue(SegmentStartSecProperty, value); }
    public double SegmentEndSec { get => (double)GetValue(SegmentEndSecProperty); set => SetValue(SegmentEndSecProperty, value); }

    /// <summary>Vrai : 00:00 à droite et la lecture avance vers la gauche (sens de l'arabe).</summary>
    public bool TimeRightToLeft { get => (bool)GetValue(TimeRightToLeftProperty); set => SetValue(TimeRightToLeftProperty, value); }

    /// <summary>Commande appelée avec la position (secondes) choisie par clic ou glissement.</summary>
    public ICommand? SeekCommand { get => (ICommand?)GetValue(SeekCommandProperty); set => SetValue(SeekCommandProperty, value); }

    private static DependencyProperty Register(string name, Type type, object? defaultValue, PropertyChangedCallback callback, bool twoWay = false) =>
        DependencyProperty.Register(name, type, typeof(WaveformView),
            new FrameworkPropertyMetadata(defaultValue,
                FrameworkPropertyMetadataOptions.AffectsRender | (twoWay ? FrameworkPropertyMetadataOptions.BindsTwoWayByDefault : 0),
                callback));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static void OnPeaksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (WaveformView)d;
        var peaks = e.NewValue as float[];
        var max = peaks is { Length: > 0 } ? peaks.Max() : 0f;
        view._peakMax = max > 0.0001f ? max : 1f;
        view.ResetWindow();
    }

    private static void OnSegmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (WaveformView)d;
        // Pendant le glissement d'un marqueur, la vue reste fixe (sinon le marqueur fuirait sous la souris)
        if (view._dragTarget == DragTarget.None) view.ResetWindow();
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (WaveformView)d;
        view._window = FollowPosition(view._window, view.PositionSec, view.DurationSec);
    }

    private void ResetWindow() =>
        _window = FollowPosition(ComputeSegmentWindow(DurationSec, SegmentStartSec, SegmentEndSec), PositionSec, DurationSec);

    /// <summary>Fenêtre affichée : tout le fichier s'il est court, sinon le segment avec une marge.</summary>
    public static (double Start, double End) ComputeSegmentWindow(double durationSec, double segmentStartSec, double segmentEndSec)
    {
        if (durationSec <= 0) return (0, 1);
        if (durationSec <= FullViewMaxSec) return (0, durationSec);

        var start = Math.Clamp(Math.Min(segmentStartSec, segmentEndSec), 0, durationSec);
        var end = Math.Clamp(Math.Max(segmentStartSec, segmentEndSec), 0, durationSec);
        var length = end - start;
        var padding = Math.Max(4, length * 0.25);
        var width = Math.Max(MinWindowSec, length + 2 * padding);
        var windowStart = Math.Max(0, (start + end) / 2 - width / 2);
        var windowEnd = Math.Min(durationSec, windowStart + width);
        windowStart = Math.Max(0, windowEnd - width);
        return (windowStart, windowEnd);
    }

    /// <summary>
    /// Suit la tête de lecture page par page : si elle sort de la fenêtre, la fenêtre (même largeur)
    /// est déplacée pour la placer à 10 % de sa largeur. Sinon la fenêtre ne bouge pas.
    /// </summary>
    public static (double Start, double End) FollowPosition((double Start, double End) window, double positionSec, double durationSec)
    {
        if (durationSec <= FullViewMaxSec) return window;
        if (positionSec >= window.Start && positionSec <= window.End) return window;

        var width = window.End - window.Start;
        var start = Math.Clamp(positionSec - width * 0.1, 0, Math.Max(0, durationSec - width));
        return (start, Math.Min(durationSec, start + width));
    }

    /// <summary>Pas de graduation (secondes) donnant environ <paramref name="targetTicks"/> repères.</summary>
    public static double PickTickStep(double windowSec, int targetTicks = 8)
    {
        double[] steps = { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800 };
        foreach (var step in steps)
            if (windowSec / step <= targetTicks) return step;
        return 3600;
    }

    /// <summary>Nouvelle valeur d'un marqueur glissé : bornée au fichier et à l'autre marqueur.</summary>
    public static double ClampMarker(double timeSec, bool isStart, double otherMarkerSec, double durationSec)
    {
        var max = durationSec > 0 ? durationSec : double.MaxValue;
        return isStart
            ? Math.Clamp(timeSec, 0, Math.Max(0, otherMarkerSec - MinSegmentSec))
            : Math.Clamp(timeSec, Math.Min(max, otherMarkerSec + MinSegmentSec), max);
    }

    public static string FormatAxisTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    private double TimeToX(double t, double width) =>
        TimeToX(t, _window, width, TimeRightToLeft);

    private double XToTime(double x, double width) =>
        XToTime(x, _window, width, TimeRightToLeft);

    /// <summary>Position horizontale (px) d'un instant dans la fenêtre affichée.</summary>
    public static double TimeToX(double t, (double Start, double End) window, double width, bool rightToLeft)
    {
        var fraction = (t - window.Start) / Math.Max(0.001, window.End - window.Start);
        return (rightToLeft ? 1 - fraction : fraction) * width;
    }

    /// <summary>Instant correspondant à une position horizontale (px) dans la fenêtre affichée.</summary>
    public static double XToTime(double x, (double Start, double End) window, double width, bool rightToLeft)
    {
        var fraction = Math.Clamp(x / Math.Max(1, width), 0, 1);
        if (rightToLeft) fraction = 1 - fraction;
        return window.Start + fraction * (window.End - window.Start);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= AxisHeight) return;

        dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        var waveHeight = height - AxisHeight;
        var midY = waveHeight / 2;
        var segStart = Math.Min(SegmentStartSec, SegmentEndSec);
        var segEnd = Math.Max(SegmentStartSec, SegmentEndSec);
        var hasSegment = DurationSec > 0 && segEnd > segStart;

        // Segment surligné
        if (hasSegment)
        {
            var xa = TimeToX(segStart, width);
            var xb = TimeToX(segEnd, width);
            var x0 = Math.Max(0, Math.Min(xa, xb));
            var x1 = Math.Min(width, Math.Max(xa, xb));
            if (x1 > x0)
                dc.DrawRectangle(SegmentFillBrush, null, new Rect(x0, 0, x1 - x0, waveHeight));
        }

        // Onde : une barre par colonne de 2 px, maximum des points couverts par la colonne
        var peaks = Peaks;
        if (peaks is { Length: > 0 } && DurationSec > 0)
        {
            var pointsPerSec = peaks.Length / DurationSec;
            for (double x = 0; x < width; x += ColumnStep)
            {
                var ta = XToTime(x, width);
                var tb = XToTime(x + ColumnStep, width);
                var t0 = Math.Min(ta, tb);
                var t1 = Math.Max(ta, tb);
                var i0 = Math.Clamp((int)(t0 * pointsPerSec), 0, peaks.Length - 1);
                var i1 = Math.Clamp((int)Math.Ceiling(t1 * pointsPerSec), i0 + 1, peaks.Length);
                float max = 0;
                for (var i = i0; i < i1; i++) if (peaks[i] > max) max = peaks[i];

                var barHeight = Math.Max(1, max / _peakMax * (waveHeight - 6));
                var inSegment = hasSegment && t1 > segStart && t0 < segEnd;
                dc.DrawRectangle(inSegment ? SegmentWaveBrush : OutsideWaveBrush, null,
                    new Rect(x, midY - barHeight / 2, ColumnStep * 0.7, barHeight));
            }
        }
        else
        {
            dc.DrawLine(AxisPen, new Point(0, midY), new Point(width, midY));
        }

        // Marqueurs de début et de fin, avec poignée
        if (hasSegment)
        {
            foreach (var t in new[] { segStart, segEnd })
            {
                var x = TimeToX(t, width);
                if (x < -1 || x > width + 1) continue;
                dc.DrawLine(MarkerPen, new Point(x, 0), new Point(x, waveHeight));
                dc.DrawRoundedRectangle(MarkerBrush, null, new Rect(x - 4, 0, 8, 12), 2, 2);
            }
        }

        // Tête de lecture
        var px = TimeToX(PositionSec, width);
        if (DurationSec > 0 && px >= 0 && px <= width)
            dc.DrawLine(PlayheadPen, new Point(px, 0), new Point(px, waveHeight));

        // Échelle de temps
        dc.DrawLine(AxisPen, new Point(0, waveHeight), new Point(width, waveHeight));
        var span = _window.End - _window.Start;
        if (DurationSec > 0 && span > 0)
        {
            var step = PickTickStep(span);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            for (var t = Math.Ceiling(_window.Start / step) * step; t <= _window.End + 0.0001; t += step)
            {
                var x = TimeToX(t, width);
                dc.DrawLine(AxisPen, new Point(x, waveHeight), new Point(x, waveHeight + 4));
                var label = new FormattedText(FormatAxisTime(t), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    AxisTypeface, 10, AxisTextBrush, dpi);
                var lx = Math.Clamp(x - label.Width / 2, 0, Math.Max(0, width - label.Width));
                dc.DrawText(label, new Point(lx, waveHeight + 5));
            }
        }
    }

    private DragTarget HitMarker(double x)
    {
        if (DurationSec <= 0 || SegmentEndSec <= SegmentStartSec) return DragTarget.None;
        var dStart = Math.Abs(x - TimeToX(SegmentStartSec, ActualWidth));
        var dEnd = Math.Abs(x - TimeToX(SegmentEndSec, ActualWidth));
        if (Math.Min(dStart, dEnd) > MarkerGrabPx) return DragTarget.None;
        return dStart <= dEnd ? DragTarget.Start : DragTarget.End;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (DurationSec <= 0) return;
        var x = e.GetPosition(this).X;
        _dragTarget = HitMarker(x);
        if (_dragTarget == DragTarget.None)
        {
            _isScrubbing = CaptureMouse();
            Seek(x);
        }
        else if (!CaptureMouse())
        {
            _dragTarget = DragTarget.None;
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;
        if (e.LeftButton == MouseButtonState.Pressed && _dragTarget != DragTarget.None)
        {
            var t = Math.Round(XToTime(x, ActualWidth), 2);
            if (_dragTarget == DragTarget.Start)
                SetCurrentValue(SegmentStartSecProperty, ClampMarker(t, true, SegmentEndSec, DurationSec));
            else
                SetCurrentValue(SegmentEndSecProperty, ClampMarker(t, false, SegmentStartSec, DurationSec));
            return;
        }
        if (_isScrubbing && e.LeftButton == MouseButtonState.Pressed)
        {
            Seek(x);
            return;
        }
        // Survol : curseur ↔ sur un marqueur, main ailleurs
        Cursor = HitMarker(x) == DragTarget.None ? Cursors.Hand : Cursors.SizeWE;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isScrubbing && _dragTarget == DragTarget.None) return;
        _isScrubbing = false;
        _dragTarget = DragTarget.None;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _isScrubbing = false;
        _dragTarget = DragTarget.None;
    }

    private void Seek(double x)
    {
        var t = Math.Round(XToTime(x, ActualWidth), 2);
        if (SeekCommand?.CanExecute(t) == true) SeekCommand.Execute(t);
    }
}
