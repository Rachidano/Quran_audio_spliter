using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.Views.Controls;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Onde sonore du studio : fenêtre affichée (zoom), suivi de la tête de lecture, graduations et rendu.
/// </summary>
public class WaveformViewTests
{
    [Fact]
    public void ShortFile_ShowsWholeFile()
    {
        Assert.Equal((0d, 58.35), WaveformView.ComputeSegmentWindow(58.35, 46.52, 56.14));
        // La tête de lecture ne déplace jamais la vue d'un fichier court
        Assert.Equal((0d, 58.35), WaveformView.FollowPosition((0, 58.35), 70, 58.35));
    }

    [Fact]
    public void LongFile_ZoomsOnSegmentWithMargin()
    {
        // Ayah de 10 s au milieu d'Al-Baqara (2 h)
        var (start, end) = WaveformView.ComputeSegmentWindow(7200, 3600, 3610);

        Assert.Equal(WaveformView.MinWindowSec, end - start, 3);
        Assert.True(start < 3600 && end > 3610);
        Assert.Equal(3605, (start + end) / 2, 3);
    }

    [Fact]
    public void LongFile_SegmentNearStartOrEnd_StaysInsideFile()
    {
        var (s1, e1) = WaveformView.ComputeSegmentWindow(7200, 0, 5);
        Assert.Equal(0, s1);
        Assert.Equal(WaveformView.MinWindowSec, e1, 3);

        var (s2, e2) = WaveformView.ComputeSegmentWindow(7200, 7195, 7200);
        Assert.Equal(7200, e2);
        Assert.Equal(7200 - WaveformView.MinWindowSec, s2, 3);
    }

    [Fact]
    public void LongFile_PlayheadLeavingWindow_MovesOnePageKeepingWidth()
    {
        var window = (3590d, 3610d);

        // Dans la fenêtre : rien ne bouge (pas de défilement continu pendant l'écoute)
        Assert.Equal(window, WaveformView.FollowPosition(window, 3600, 7200));

        // Sortie à droite : la tête est replacée à 10 % de la largeur
        var (start, end) = WaveformView.FollowPosition(window, 3615, 7200);
        Assert.Equal(20, end - start, 3);
        Assert.Equal(3613, start, 3);
    }

    [Theory]
    [InlineData(58.35, 10)]
    [InlineData(20, 5)]
    [InlineData(4, 0.5)]
    [InlineData(7200, 900)]
    public void TickStep_GivesReadableScale(double windowSec, double expectedStep)
    {
        Assert.Equal(expectedStep, WaveformView.PickTickStep(windowSec));
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(58.35, 2918)]
    [InlineData(7200, 360000)]
    [InlineData(15482, 774100)]
    [InlineData(100000, 1000000)]
    public void PeaksResolution_Is50PointsPerSecond(double durationSec, int expectedPoints)
    {
        Assert.Equal(expectedPoints, WaveformService.PointsForDuration(durationSec));
    }

    [Fact]
    public void TimeAxis_RightToLeftByDefault_LeftToRightOnRequest()
    {
        var window = (0d, 60d);
        // Droite à gauche (défaut) : 00:00 au bord droit, fin du fichier au bord gauche
        Assert.Equal(600, WaveformView.TimeToX(0, window, 600, rightToLeft: true), 3);
        Assert.Equal(0, WaveformView.TimeToX(60, window, 600, rightToLeft: true), 3);
        Assert.Equal(450, WaveformView.TimeToX(15, window, 600, rightToLeft: true), 3);
        Assert.Equal(15, WaveformView.XToTime(450, window, 600, rightToLeft: true), 3);
        // Gauche à droite
        Assert.Equal(150, WaveformView.TimeToX(15, window, 600, rightToLeft: false), 3);
        Assert.Equal(15, WaveformView.XToTime(150, window, 600, rightToLeft: false), 3);
        Assert.True(new WaveformViewStub().DefaultIsRightToLeft);
    }

    private sealed class WaveformViewStub
    {
        public bool DefaultIsRightToLeft => (bool)WaveformView.TimeRightToLeftProperty.DefaultMetadata.DefaultValue;
    }

    [Fact]
    public void Render_DrawsSegmentInColorAndOutsideInGray()
    {
        var pixels = RunOnSta(() =>
        {
            // Onde synthétique : 60 s, amplitude forte partout
            var peaks = Enumerable.Repeat(0.8f, 3000).ToArray();
            var view = new WaveformView
            {
                Peaks = peaks, DurationSec = 60, PositionSec = 0,
                SegmentStartSec = 30, SegmentEndSec = 45, Width = 600, Height = 130
            };
            view.Measure(new Size(600, 130));
            view.Arrange(new Rect(0, 0, 600, 130));
            var bitmap = new RenderTargetBitmap(600, 130, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var data = new byte[600 * 130 * 4];
            bitmap.CopyPixels(data, 600 * 4, 0);
            return data;
        });

        Color At(int x, int y)
        {
            var i = (y * 600 + x) * 4;
            return Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
        }

        // Temps de droite à gauche : 30 s -> x = 300, 45 s -> x = 150 ; 10 s -> x = 500
        var mid = (130 - 20) / 2;
        var inside = At(220, mid);
        var outside = At(500, mid);
        Assert.Equal(Color.FromRgb(0x0d, 0x94, 0x88), inside);   // segment : sarcelle
        Assert.Equal(Color.FromRgb(0xcb, 0xd5, 0xe1), outside);  // hors segment : gris
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result;
    }
}
