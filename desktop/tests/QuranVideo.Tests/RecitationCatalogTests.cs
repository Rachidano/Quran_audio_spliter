using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NAudio.Wave;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public sealed class RecitationCatalogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "quran_catalog_" + Guid.NewGuid().ToString("N"));
    private readonly WorkspaceService _workspace = new();
    private readonly RecitationCatalogService _catalog;
    public RecitationCatalogTests()
    {
        Assert.True(_workspace.InitializeWorkspace(_folder).Success);
        _catalog = new(_workspace);
    }

    [Fact]
    public async Task DefaultHas114ExactWebLinksWithoutRepositoryOrExternalJson()
    {
        await _catalog.InitializeAsync();
        Assert.Equal(RecitationCatalogService.SaberCollection, await _catalog.GetDefaultAsync());
        var cols = await _catalog.GetCollectionsAsync();
        Assert.Equal("الشيخ صابر عبد الحكم", cols.First(c => c.CollectionId == RecitationCatalogService.SaberCollection).Name);
        Assert.Contains(cols, c => c.CollectionId == RecitationCatalogService.HusaryCollection);
        var links = await _catalog.GetLinksAsync(RecitationCatalogService.SaberCollection);
        Assert.Equal(Enumerable.Range(1, 114), links.Keys.OrderBy(x => x));
        Assert.All(links.Values, url => Assert.StartsWith("https://archive.org/download/z0603_202406x/", url));
        Assert.Contains("001  الفاتحة.mp3", Uri.UnescapeDataString(links[1]));
        Assert.Contains("002  البقرة.mp3", Uri.UnescapeDataString(links[2]));
        Assert.Equal(114, links.Values.Distinct().Count());
    }

    [Fact]
    public async Task WarshReferential_AllReciterHave114Mp3QuranLinksAndTimingReadId()
    {
        await _catalog.InitializeAsync();
        var collections = await _catalog.GetCollectionsAsync();

        Assert.Equal(4, WarshRecitationReferential.All.Count);
        // Minutages vérifiés conformes au compte Warsh : الحصري 120، القزابري 80، الكوشي 16، ياسين 14.
        Assert.Equal(new int?[] { 120, 80, 16, 14 }, WarshRecitationReferential.All.Select(r => r.TimingReadId));
        foreach (var reference in WarshRecitationReferential.All)
        {
            var choice = Assert.Single(collections, c => c.CollectionId == reference.CollectionId);
            Assert.Equal(reference.TitleArabic, choice.Name);

            var links = await _catalog.GetLinksAsync(reference.CollectionId);
            Assert.Equal(Enumerable.Range(1, 114), links.Keys.OrderBy(x => x));
            Assert.All(links, l => Assert.Equal(string.Format(reference.UrlTemplate, l.Key), l.Value));
            Assert.All(links.Values, url => Assert.StartsWith("https://cdn.mp3quran.net/audio/", url));

            using var db = new AppDbContext(_workspace.GetDatabasePath());
            var entity = await db.RecitationCollections.SingleAsync(c => c.Id == reference.CollectionId);
            Assert.Equal(reference.TimingReadId, entity.TimingReadId);
            Assert.Equal("warsh", entity.Riwaya);
        }

        // Saber reste le récitateur par défaut, en tête de liste
        Assert.Equal(RecitationCatalogService.SaberCollection, collections[0].CollectionId);
        Assert.Equal(RecitationCatalogService.HusaryCollection, collections[1].CollectionId);
    }

    [Fact]
    public async Task WarshReferential_RenamedReciterAndCustomLinkSurviveReinitialization()
    {
        await _catalog.InitializeAsync();
        await _catalog.RenameAsync("qazabri_warsh", "القزابري");
        await _catalog.SaveLinksAsync("qazabri_warsh", new Dictionary<int, string> { [2] = "https://example.org/baqara.mp3" });

        await new RecitationCatalogService(_workspace).InitializeAsync();

        Assert.Equal("القزابري", (await _catalog.GetCollectionsAsync()).Single(c => c.CollectionId == "qazabri_warsh").Name);
        var links = await _catalog.GetLinksAsync("qazabri_warsh");
        Assert.Equal("https://example.org/baqara.mp3", links[2]);
        Assert.Equal("https://cdn.mp3quran.net/audio/omar-kazabri/r1/001.mp3", links[1]);
    }

    [Fact]
    public async Task HusaryLegacyQuranicAudioLinks_AreReplacedByMp3QuranWarsh_ButCustomLinkIsKept()
    {
        // Espace créé par une ancienne version : liens quranicaudio.com (aujourd'hui en 404)
        await _catalog.InitializeAsync();
        using (var db = new AppDbContext(_workspace.GetDatabasePath()))
        {
            var legacy = RecitationCatalogService.LegacyHusaryUrlPrefix;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE recitation_collections SET BaseUrlTemplate = {legacy + "{0:D3}.mp3"} WHERE Id = {RecitationCatalogService.HusaryCollection}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audio_download_links SET Url = {legacy} || printf('%03d', SurahNumber) || '.mp3' WHERE CollectionId = {RecitationCatalogService.HusaryCollection}");
        }
        await _catalog.SaveLinksAsync(RecitationCatalogService.HusaryCollection, new Dictionary<int, string> { [5] = "https://example.org/husary-005.mp3" });

        await new RecitationCatalogService(_workspace).InitializeAsync();

        var links = await _catalog.GetLinksAsync(RecitationCatalogService.HusaryCollection);
        Assert.Equal("https://cdn.mp3quran.net/audio/mahmoud-husary/r3/001.mp3", links[1]);
        Assert.Equal("https://cdn.mp3quran.net/audio/mahmoud-husary/r3/114.mp3", links[114]);
        Assert.Equal("https://example.org/husary-005.mp3", links[5]);
        Assert.DoesNotContain(links.Values, url => url.Contains("quranicaudio", StringComparison.OrdinalIgnoreCase));
        using var verify = new AppDbContext(_workspace.GetDatabasePath());
        var husary = await verify.RecitationCollections.SingleAsync(c => c.Id == RecitationCatalogService.HusaryCollection);
        Assert.Equal(RecitationCatalogService.HusaryUrlTemplate, husary.BaseUrlTemplate);
    }

    [Fact]
    public async Task EditedAndBlankLinksAndDefaultSurviveReinitialization()
    {
        await _catalog.InitializeAsync();
        await _catalog.SaveLinksAsync(RecitationCatalogService.SaberCollection, new Dictionary<int, string> { [1] = "https://example.org/custom.mp3", [2] = "" });
        var other = await _catalog.AddReciterAsync("قارئ آخر");
        await _catalog.SetDefaultAsync(other.CollectionId);
        await _catalog.RenameAsync(other.CollectionId, "الاسم المعدل");
        var reloaded = new RecitationCatalogService(_workspace);
        await reloaded.InitializeAsync();
        var links = await reloaded.GetLinksAsync(RecitationCatalogService.SaberCollection);
        Assert.Equal("https://example.org/custom.mp3", links[1]);
        Assert.Equal("", links[2]);
        Assert.Equal(other.CollectionId, await reloaded.GetDefaultAsync());
        Assert.Equal("الاسم المعدل", (await reloaded.GetCollectionsAsync()).Single(c => c.CollectionId == other.CollectionId).Name);
        Assert.All((await reloaded.GetLinksAsync(other.CollectionId)).Values, url => Assert.Empty(url));
    }

    [Fact]
    public async Task UpgradePreservesExistingCollectionAndItsAudio()
    {
        using (var db = new AppDbContext(_workspace.GetDatabasePath()))
        {
            await db.Database.EnsureCreatedAsync();
            db.Reciters.Add(new ReciterEntity { Id = "existing", NameArabic = "قديم" });
            db.RecitationCollections.Add(new RecitationCollectionEntity { Id = "existing_warsh", ReciterId = "existing", BaseUrlTemplate = "https://example.org/{0:D3}.mp3" });
            db.AudioSources.Add(new AudioSourceEntity { Id = "existing_warsh:001", CollectionId = "existing_warsh", SurahNumber = 1, RelativeFilePath = "media/old.mp3" });
            await db.SaveChangesAsync();
        }
        await _catalog.InitializeAsync();
        Assert.Equal("https://example.org/002.mp3", (await _catalog.GetLinksAsync("existing_warsh"))[2]);
        Assert.NotNull(await new RecitationLibraryService(_workspace).GetAudioSourceAsync("existing_warsh", 1));
    }

    [Theory]
    [InlineData("file:///C:/secret.mp3")]
    [InlineData("not a link")]
    [InlineData("https://user:password@example.org/a.mp3")]
    public async Task InvalidLinkRejectsWholeSave(string invalid)
    {
        await _catalog.InitializeAsync();
        var before = await _catalog.GetLinksAsync(RecitationCatalogService.SaberCollection);
        await Assert.ThrowsAsync<ArgumentException>(() => _catalog.SaveLinksAsync(RecitationCatalogService.SaberCollection,
            new Dictionary<int, string> { [1] = "https://example.org/changed.mp3", [2] = invalid }));
        Assert.Equal(before[1], (await _catalog.GetLinksAsync(RecitationCatalogService.SaberCollection))[1]);
    }

    [Fact]
    public async Task DownloadUsesEditedLinkAndRealAudioMetadata()
    {
        await _catalog.InitializeAsync();
        const string url = "https://example.org/edited-surah.mp3";
        await _catalog.SaveLinksAsync(RecitationCatalogService.SaberCollection, new Dictionary<int, string> { [2] = url });
        var handler = new AudioHandler(Wave(22050, 1));
        using var client = new HttpClient(handler);
        var result = await new RecitationLibraryService(_workspace, client).DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 2);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(url, handler.RequestedUrl);
        Assert.Equal(url, result.Source!.SourceUrl);
        Assert.Equal(22050, result.Source.SampleRate);
        Assert.Equal(1, result.Source.Channels);
        Assert.InRange(result.Source.DurationSeconds, .99, 1.01);
        Assert.True(File.Exists(_workspace.ToAbsolutePath(result.Source.RelativeFilePath)));
    }

    [Fact]
    public async Task InvalidResponsePreservesPreviouslyDownloadedSource()
    {
        await _catalog.InitializeAsync();
        var handler = new AudioHandler(Wave(22050, 1));
        using var client = new HttpClient(handler);
        var service = new RecitationLibraryService(_workspace, client);
        var original = await service.DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 1);
        Assert.True(original.Success, original.ErrorMessage);
        handler.Bytes = Encoding.UTF8.GetBytes("not audio");
        var invalid = await service.DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 1);
        Assert.False(invalid.Success);
        var retained = await service.GetAudioSourceAsync(RecitationCatalogService.SaberCollection, 1);
        Assert.Equal(original.Source!.Sha256, retained!.Sha256);
        Assert.True(File.Exists(_workspace.ToAbsolutePath(retained.RelativeFilePath)));
        Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "media", "audio", "downloads")));
    }

    [Fact]
    public async Task TwoRecitersHaveIndependentLinksAndAudioFiles()
    {
        await _catalog.InitializeAsync();
        var other = await _catalog.AddReciterAsync("الثاني");
        await _catalog.SaveLinksAsync(other.CollectionId, new Dictionary<int, string> { [1] = "https://example.org/other.mp3" });
        var handler = new AudioHandler(Wave(22050, 1));
        using var client = new HttpClient(handler);
        var service = new RecitationLibraryService(_workspace, client);
        var first = await service.DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 1);
        var second = await service.DownloadSurahAudioAsync(other.CollectionId, 1);
        Assert.True(first.Success, first.ErrorMessage); Assert.True(second.Success, second.ErrorMessage);
        Assert.NotEqual(first.Source!.RelativeFilePath, second.Source!.RelativeFilePath);
        Assert.NotEqual(first.Source.SourceUrl, second.Source.SourceUrl);
        Assert.Null(await service.GetAudioSourceAsync(other.CollectionId, 2));
    }

    [Fact]
    public async Task BlankLinkDoesNotFallBackToDefaultReciter()
    {
        await _catalog.InitializeAsync();
        var other = await _catalog.AddReciterAsync("بلا روابط");
        var handler = new AudioHandler(Wave(22050, 1));
        using var client = new HttpClient(handler);
        var result = await new RecitationLibraryService(_workspace, client).DownloadSurahAudioAsync(other.CollectionId, 1);
        Assert.False(result.Success);
        Assert.Null(handler.RequestedUrl);
    }

    [Fact]
    public async Task CancelledDownloadLeavesNoSourceOrPartialFile()
    {
        await _catalog.InitializeAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var client = new HttpClient(new AudioHandler(Wave(22050, 1)));
        var service = new RecitationLibraryService(_workspace, client);
        var result = await service.DownloadSurahAudioAsync(RecitationCatalogService.SaberCollection, 1, cancellationToken: cancellation.Token);
        Assert.False(result.Success);
        Assert.Null(await service.GetAudioSourceAsync(RecitationCatalogService.SaberCollection, 1));
        Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "media", "audio", "downloads")));
    }

    [Fact]
    public async Task AnnotationNeverSubstitutesAnotherReciterForMissingSource()
    {
        await _catalog.InitializeAsync();
        using (var db = new AppDbContext(_workspace.GetDatabasePath()))
        {
            db.AudioSources.Add(new AudioSourceEntity { Id = "saber_abdelhakam_warsh:001", CollectionId = RecitationCatalogService.SaberCollection, SurahNumber = 1, DurationSeconds = 60 });
            await db.SaveChangesAsync();
        }
        var passage = new AyahPassageEntity { AudioSourceId = "another:001", SurahNumber = 1, AyahNumber = 1, AyahId = "1:1", StartTimeSec = 1, EndTimeSec = 2 };
        var result = await new AnnotationService(_workspace).SavePassageAsync(passage, 60);
        Assert.False(result.Success);
        Assert.Equal("another:001", passage.AudioSourceId);
    }

    private static byte[] Wave(int sampleRate, int channels)
    {
        using var memory = new MemoryStream();
        using (var writer = new WaveFileWriter(memory, new WaveFormat(sampleRate, 16, channels)))
            for (int i = 0; i < sampleRate; i++) writer.WriteSample((float)Math.Sin(i * .1) * .1f);
        return memory.ToArray();
    }
    private sealed class AudioHandler(byte[] bytes) : HttpMessageHandler
    {
        public byte[] Bytes { get; set; } = bytes;
        public string? RequestedUrl { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedUrl = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) });
        }
    }
    public void Dispose()
    {
        _workspace.Dispose(); SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, true);
    }
}
