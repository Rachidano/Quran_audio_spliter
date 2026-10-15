using System.IO;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class AudioTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly RecitationLibraryService _recitationService;
    private readonly WaveformService _waveformService;

    public AudioTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_audio_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _recitationService = new RecitationLibraryService(_workspaceService);
        _waveformService = new WaveformService(_workspaceService);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task InitializeDefaultCollection_CreatesSaberReciterAndCollection()
    {
        await _recitationService.InitializeDefaultCollectionAsync();

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        var reciter = await db.Reciters.FirstOrDefaultAsync(r => r.Id == "saber_abdelhakam");
        Assert.NotNull(reciter);
        Assert.Equal("الشيخ صابر عبد الحكم", reciter.NameArabic);

        var collection = await db.RecitationCollections.FirstOrDefaultAsync(c => c.Id == RecitationCatalogService.SaberCollection);
        Assert.NotNull(collection);
        Assert.Equal("warsh", collection.Riwaya);
        Assert.Equal(114, (await new RecitationCatalogService(_workspaceService).GetLinksAsync(collection.Id)).Count);
    }

    [Fact]
    public async Task ImportLocalAudioAsync_ImportsFatihaAndExtractsMetadata()
    {
        await _recitationService.InitializeDefaultCollectionAsync();

        var fatihaAudio = Path.GetFullPath("data/audio/recitations/warsh/husary/001.mp3");
        if (!File.Exists(fatihaAudio)) return;

        var (success, source, error) = await _recitationService.ImportLocalAudioAsync(
            RecitationCatalogService.SaberCollection,
            surahNumber: 1,
            localSourceFilePath: fatihaAudio);

        Assert.True(success, error);
        Assert.NotNull(source);
        Assert.Equal(1, source.SurahNumber);
        Assert.True(source.DurationSeconds >= 60.0 && source.DurationSeconds <= 75.0);
        Assert.False(string.IsNullOrWhiteSpace(source.Sha256));
        Assert.False(Path.IsPathRooted(source.RelativeFilePath));

        var targetFile = _workspaceService.ToAbsolutePath(source.RelativeFilePath);
        Assert.True(File.Exists(targetFile));
    }

    [Fact]
    public async Task WaveformService_ComputesAndCachesPeaks()
    {
        var fatihaAudio = Path.GetFullPath("data/audio/recitations/warsh/husary/001.mp3");
        if (!File.Exists(fatihaAudio)) return;

        var dummyHash = "fatiha_test_hash_001";
        var peaks1 = await _waveformService.GetWaveformPeaksAsync(fatihaAudio, dummyHash, totalPoints: 500);

        Assert.Equal(500, peaks1.Length);
        Assert.Contains(peaks1, p => p > 0.05f);
        Assert.All(peaks1, p => Assert.InRange(p, 0.0f, 1.0f));

        // Verify cached file was written
        var cacheFile = Path.Combine(_testWorkspaceDir, "cache", "peaks", $"{dummyHash}_500.dat");
        Assert.True(File.Exists(cacheFile));

        // Second call loads from cache
        var peaks2 = await _waveformService.GetWaveformPeaksAsync(fatihaAudio, dummyHash, totalPoints: 500);
        Assert.Equal(peaks1, peaks2);
    }

    [Fact]
    public void AudioPlayerService_LoadsAndControlsAudioPosition()
    {
        var fatihaAudio = @"D:\Projets\ITComp\Quran\data\audio\sources\001\original.mp3";
        if (!File.Exists(fatihaAudio))
        {
            fatihaAudio = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\data\audio\sources\001\original.mp3"));
        }
        Assert.True(File.Exists(fatihaAudio), $"Fichier de test manquant: {fatihaAudio}");

        using var player = new AudioPlayerService();
        player.Load(fatihaAudio);

        Assert.True(player.DurationSec > 60.0);
        Assert.Equal(0.0, player.CurrentTimeSec, precision: 1);

        player.Seek(15.5);
        Assert.Equal(15.5, player.CurrentTimeSec, precision: 1);

        player.StepForward(2.0);
        Assert.Equal(17.5, player.CurrentTimeSec, precision: 1);

        player.StepBackward(5.0);
        Assert.Equal(12.5, player.CurrentTimeSec, precision: 1);

        // Test seeking backward multiple times
        player.Seek(30.0);
        player.Seek(10.0);
        Assert.Equal(10.0, player.CurrentTimeSec, precision: 1);
        player.Seek(2.0);
        Assert.Equal(2.0, player.CurrentTimeSec, precision: 1);

        // Test seeking backward during active playback
        player.Seek(0);
        player.Play();
        Thread.Sleep(500);
        player.Seek(10.0);
        Thread.Sleep(500);
        player.StepBackward(5.0);
        Thread.Sleep(500);
        Assert.True(player.CurrentTimeSec >= 5.0 && player.CurrentTimeSec <= 7.0, $"CurrentTime was {player.CurrentTimeSec}");
        player.Pause();
    }

    [Fact]
    public async Task AudioCatalog_DefaultReciterIsSaberHakam()
    {
        var catalog = new RecitationCatalogService(_workspaceService);
        await catalog.InitializeAsync();
        var defaultId = await catalog.GetDefaultAsync();
        var selected = (await catalog.GetCollectionsAsync()).Single(c => c.CollectionId == defaultId);
        Assert.Equal("saber_abdelhakam", selected.ReciterId);
        Assert.Equal("الشيخ صابر عبد الحكم", selected.Name);
        Assert.Equal(RecitationCatalogService.SaberCollection, selected.CollectionId);
    }

    [Fact]
    public async Task AudioCatalog_SaveAndReloadConfiguration()
    {
        var catalog = new RecitationCatalogService(_workspaceService);
        await catalog.InitializeAsync();
        await catalog.RenameAsync(RecitationCatalogService.SaberCollection, "الشيخ صابر عبد الحكم (رواية ورش)");
        await catalog.SaveLinksAsync(RecitationCatalogService.SaberCollection, new Dictionary<int,string> { [1] = "https://mycustomserver.org/audio/001.mp3" });
        var reloaded = new RecitationCatalogService(_workspaceService);
        Assert.Equal("الشيخ صابر عبد الحكم (رواية ورش)", (await reloaded.GetCollectionsAsync()).First(c => c.CollectionId == RecitationCatalogService.SaberCollection).Name);
        Assert.Equal("https://mycustomserver.org/audio/001.mp3", (await reloaded.GetLinksAsync(RecitationCatalogService.SaberCollection))[1]);
    }

    [Fact]
    public async Task AudioCatalog_ResolvesWebProjectUrlsWithoutLocalAudioFallback()
    {
        var catalog = new RecitationCatalogService(_workspaceService);
        await catalog.InitializeAsync();
        var links = await catalog.GetLinksAsync(RecitationCatalogService.SaberCollection);
        Assert.Contains("archive.org/download/z0603_202406x", links[1]);
        Assert.Contains("archive.org/download/z0603_202406x", links[114]);
        Assert.Null(await _recitationService.GetAudioSourceAsync(RecitationCatalogService.SaberCollection, 1));
    }
}
