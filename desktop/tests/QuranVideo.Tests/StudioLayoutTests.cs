using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using QuranVideo.Desktop.Views.Controls;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Nouvelle disposition du studio : saisie des temps au clavier, listes déroulantes vidéo,
/// tableau des segments, marqueurs déplaçables et affichage des heures.
/// </summary>
public class StudioLayoutTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly StudioViewModel _vm;

    public StudioLayoutTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"studio_layout_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _vm = new StudioViewModel(_workspaceService);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Theory]
    [InlineData("46.52", 46.52)]
    [InlineData("00:46.52", 46.52)]
    [InlineData("0:46,52", 46.52)]
    [InlineData("1:02:05.30", 3725.3)]
    [InlineData("12:00", 720)]
    [InlineData("46٫5", 46.5)]
    public void TryParseTime_AcceptsCommonFormats(string text, double expected)
    {
        Assert.True(StudioViewModel.TryParseTime(text, out var seconds));
        Assert.Equal(expected, seconds, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:75")]
    [InlineData("1.5:20")]
    [InlineData("-3")]
    [InlineData("1:2:3:4")]
    public void TryParseTime_RejectsInvalidInput(string text)
    {
        Assert.False(StudioViewModel.TryParseTime(text, out _));
    }

    [Fact]
    public void TypingTimes_UpdatesSegment_AndRejectsInvalidValue()
    {
        _vm.AudioDurationSec = 58.35;

        _vm.StartTimeText = "00:46.52";
        _vm.EndTimeText = "56.14";

        Assert.Equal(46.52, _vm.StartTimeSec, 3);
        Assert.Equal(56.14, _vm.EndTimeSec, 3);
        Assert.Equal("00:09.62", _vm.SegmentDurationText);

        _vm.EndTimeText = "03:00";   // au-delà du fichier : refusé, valeur conservée
        Assert.Equal(56.14, _vm.EndTimeSec, 3);
        Assert.Equal("00:56.14", _vm.EndTimeText);
        Assert.Contains("صيغة الوقت", _vm.StatusMessage);
    }

    [Fact]
    public void MovingMarkers_UpdatesTypedTimes()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _vm.StartTimeSec = 10;
        _vm.EndTimeSec = 15.5;

        Assert.Equal("00:10.00", _vm.StartTimeText);
        Assert.Equal("00:05.50", _vm.SegmentDurationText);
        Assert.Contains(nameof(StudioViewModel.StartTimeText), changed);
        Assert.Contains(nameof(StudioViewModel.EndTimeText), changed);
        Assert.Contains(nameof(StudioViewModel.SegmentDurationText), changed);
    }

    [Fact]
    public void VideoDropdowns_MapToSettings()
    {
        _vm.OrientationIndex = 1;
        Assert.True(_vm.IsVertical);
        _vm.OrientationIndex = 0;
        Assert.True(_vm.IsHorizontal);

        _vm.BackgroundIndex = 1;
        Assert.Equal(BackgroundType.Dark, _vm.StudioBackground);
        _vm.StudioCustomBackgroundPath = "media/assets/fond.png";
        _vm.BackgroundIndex = 2;     // image déjà choisie : pas de boîte de dialogue
        Assert.Equal(BackgroundType.CustomImage, _vm.StudioBackground);
        _vm.BackgroundIndex = 0;
        Assert.Equal(BackgroundType.Light, _vm.StudioBackground);
    }

    [Fact]
    public void WatermarkDropdown_TextOpensSettingsWhenEmpty_LogoReusesChosenFile()
    {
        _vm.WatermarkIndex = 1;
        Assert.Equal(WatermarkType.Text, _vm.WizardWatermark);
        Assert.True(_vm.IsVideoSettingsOpen);   // pour saisir le texte

        _vm.CloseVideoSettings();
        _vm.WizardWatermarkImagePath = "media/assets/logo.png";
        _vm.WatermarkIndex = 2;
        Assert.Equal(WatermarkType.Logo, _vm.WizardWatermark);
        Assert.False(_vm.IsVideoSettingsOpen);

        _vm.WatermarkIndex = 0;
        Assert.Equal(WatermarkType.None, _vm.WizardWatermark);
        Assert.Equal(0, _vm.WatermarkIndex);
    }

    [Fact]
    public void FormatTime_ShowsHoursForLongSurahs()
    {
        Assert.Equal("00:46.52", StudioViewModel.FormatTime(46.52));
        Assert.Equal("1:00:05.30", StudioViewModel.FormatTime(3605.3));
        Assert.Equal("1:00:05.30", AyahPassageEntity.FormatClock(3605.3));
    }

    [Fact]
    public void PassageTable_StatusLabels()
    {
        Assert.Equal("مقترح", new AyahPassageEntity { Notes = "mp3quran:120", IsValidatedByUser = false }.StatusLabel);
        Assert.Equal("مسجّل", new AyahPassageEntity { Kind = PassageKind.Full, IsValidatedByUser = true }.StatusLabel);
        Assert.Equal("إعادة كاملة", new AyahPassageEntity { Kind = PassageKind.FullRetake, IsValidatedByUser = true }.StatusLabel);
        var passage = new AyahPassageEntity { StartTimeSec = 3600, EndTimeSec = 3611.5 };
        Assert.Equal("1:00:00.00", passage.FormattedStartTime);
        Assert.Equal("1:00:11.50", passage.FormattedEndTime);
    }

    [Fact]
    public void DraggedMarkers_StayInsideFileAndNeverCross()
    {
        // Début : jamais avant 0, jamais après (fin - 50 ms)
        Assert.Equal(0, WaveformView.ClampMarker(-3, isStart: true, otherMarkerSec: 20, durationSec: 60));
        Assert.Equal(19.95, WaveformView.ClampMarker(25, isStart: true, otherMarkerSec: 20, durationSec: 60), 3);
        // Fin : jamais avant (début + 50 ms), jamais après la fin du fichier
        Assert.Equal(10.05, WaveformView.ClampMarker(5, isStart: false, otherMarkerSec: 10, durationSec: 60), 3);
        Assert.Equal(60, WaveformView.ClampMarker(75, isStart: false, otherMarkerSec: 10, durationSec: 60));
        Assert.Equal(33.3, WaveformView.ClampMarker(33.3, isStart: false, otherMarkerSec: 10, durationSec: 60), 3);
    }

    [Fact]
    public void EqualsMultiConverter_HighlightsCurrentAyahRow()
    {
        var converter = new EqualsMultiConverter();
        Assert.Equal(true, converter.Convert(new object[] { 7, 7 }, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.Convert(new object[] { 6, 7 }, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
