using System.Diagnostics;
using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Fichier texte de publication (titre + description prêts à copier) écrit à côté de chaque vidéo.
/// </summary>
public class PublicationTextTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;

    public PublicationTextTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_publication_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        new RecitationCatalogService(_workspaceService).InitializeAsync().GetAwaiter().GetResult();

        using var db = new AppDbContext(_workspaceService.GetDatabasePath());
        db.AudioSources.Add(new AudioSourceEntity
        {
            Id = RecitationCatalogService.HusaryCollection + ":002",
            CollectionId = RecitationCatalogService.HusaryCollection,
            SurahNumber = 2,
            RelativeFilePath = "media/audio/husary/002.mp3"
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void BuildText_SingleAyah_HasTitleAndDescriptionReadyToCopy()
    {
        var text = PublicationTextService.BuildText("البقرة", 255, 255, "الشيخ محمود خليل الحصري");

        Assert.Contains("سورة البقرة - الآية 255 | الشيخ محمود خليل الحصري | رواية ورش عن نافع", text);
        Assert.Contains("سورة البقرة — الآية 255", text);
        Assert.Contains("القارئ: الشيخ محمود خليل الحصري", text);
        Assert.Contains("الرواية: ورش عن نافع", text);
        Assert.DoesNotContain("البوابة الرسمية", text);
        Assert.DoesNotContain("مصحف الجزائر", text);
        Assert.Contains("#سورة_البقرة", text);
    }

    [Fact]
    public void BuildText_ReciterNameWithRiwaya_DoesNotRepeatRiwaya()
    {
        var text = PublicationTextService.BuildText("الفاتحة", 1, 1, "المصحف المرتل برواية ورش — محمود خليل الحصري");

        Assert.Contains("سورة الفاتحة - الآية 1 | محمود خليل الحصري | رواية ورش عن نافع", text);
        Assert.DoesNotContain("المصحف المرتل", text);
    }

    [Fact]
    public void BuildText_AyahRange_AndMultiWordSurahHashtag()
    {
        var text = PublicationTextService.BuildText("آل عمران", 1, 5, null);

        Assert.Contains("سورة آل عمران - الآيات 1 إلى 5 | رواية ورش عن نافع", text);
        Assert.DoesNotContain("القارئ:", text);
        Assert.Contains("#سورة_آل_عمران", text);
    }

    [Fact]
    public async Task WriteForVideo_ResolvesReciterAndWritesUtf8WithBom()
    {
        var videoPath = Path.Combine(_testWorkspaceDir, "media", "videos", "ayah_002_255_husary_h.mp4");

        var textPath = await new PublicationTextService(_workspaceService)
            .WriteForVideoAsync(videoPath, 2, 255, 255, RecitationCatalogService.HusaryCollection + ":002");

        Assert.Equal(Path.ChangeExtension(videoPath, ".txt"), textPath);
        var bytes = await File.ReadAllBytesAsync(textPath!);
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("سورة البقرة - الآية 255 | الشيخ محمود خليل الحصري | رواية ورش عن نافع", await File.ReadAllTextAsync(textPath!));
    }

    [Fact]
    public async Task WriteForVideo_UnknownAudioSource_StillWritesTextWithoutReciter()
    {
        var videoPath = Path.Combine(_testWorkspaceDir, "media", "videos", "ayah_001_001.mp4");

        var textPath = await new PublicationTextService(_workspaceService)
            .WriteForVideoAsync(videoPath, 1, 1, 1, "inconnu:001");

        Assert.NotNull(textPath);
        Assert.Contains("سورة الفاتحة - الآية 1 | رواية ورش عن نافع", await File.ReadAllTextAsync(textPath!));
    }
}

/// <summary>
/// Délai maximal d'inactivité des outils externes (FFmpeg / FFprobe), testé avec de vrais processus Windows.
/// </summary>
public class ExternalProcessRunnerTests
{
    private static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public async Task SilentProcess_IsKilledAfterInactivityTimeout()
    {
        var watch = Stopwatch.StartNew();

        // ~30 s sans aucune sortie, comme un FFmpeg bloqué sur un fichier corrompu
        var result = await ExternalProcessRunner.RunAsync(Cmd, new[] { "/c", "ping -n 30 127.0.0.1 >nul" }, TimeSpan.FromSeconds(1));

        Assert.True(result.TimedOut);
        Assert.False(result.Success);
        Assert.Contains("ثانية", result.StandardError);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"Arrêt trop tardif : {watch.Elapsed}");
    }

    [Fact]
    public async Task ProcessWritingRegularly_IsNotKilledEvenIfLongerThanTimeout()
    {
        // ~4 s au total, mais une ligne écrite chaque seconde : jamais inactif plus de 3 s
        var result = await ExternalProcessRunner.RunAsync(
            Cmd, new[] { "/c", "for /l %i in (1,1,4) do @(echo %i & ping -n 2 127.0.0.1 >nul)" }, TimeSpan.FromSeconds(3));

        Assert.False(result.TimedOut);
        Assert.True(result.Success, result.StandardError);
        Assert.Contains("4", result.StandardOutput);
    }

    [Fact]
    public async Task NonZeroExitCode_IsReportedAsFailureWithoutTimeout()
    {
        var result = await ExternalProcessRunner.RunAsync(Cmd, new[] { "/c", "exit 3" }, TimeSpan.FromSeconds(10));

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal(3, result.ExitCode);
    }

    [Fact]
    public async Task Cancellation_StopsProcessAndThrows()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExternalProcessRunner.RunAsync(Cmd, new[] { "/c", "ping -n 30 127.0.0.1 >nul" }, TimeSpan.FromMinutes(1), cts.Token));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"Annulation trop lente : {watch.Elapsed}");
    }
}
