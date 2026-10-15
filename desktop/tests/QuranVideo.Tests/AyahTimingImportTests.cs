using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Import des minutages publiés (mp3quran.net, الحصري ورش read=120) comme prises « مقترح ».
/// </summary>
public class AyahTimingImportTests : IDisposable
{
    private const string Collection = RecitationCatalogService.HusaryCollection;
    private const string SourceId = Collection + ":001";

    // Vraies valeurs mp3quran.net pour الحصري ورش, Al-Fatiha (fichier r3/001.mp3 : 58,36 s)
    private const string FatihaJson = """
        [{"ayah":1,"polygon":"","start_time":13240,"end_time":18720,"x":"1","y":"1","page":"p"},
         {"ayah":2,"polygon":"","start_time":18720,"end_time":23620,"x":"1","y":"1","page":"p"},
         {"ayah":3,"polygon":"","start_time":23620,"end_time":28440,"x":"1","y":"1","page":"p"},
         {"ayah":4,"polygon":"","start_time":28440,"end_time":35480,"x":"1","y":"1","page":"p"},
         {"ayah":5,"polygon":"","start_time":35480,"end_time":41040,"x":"1","y":"1","page":"p"},
         {"ayah":6,"polygon":"","start_time":41040,"end_time":46520,"x":"1","y":"1","page":"p"},
         {"ayah":7,"polygon":"","start_time":46520,"end_time":56140,"x":"1","y":"1","page":"p"}]
        """;

    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly string _dbPath;
    private readonly FakeHandler _handler = new();

    public AyahTimingImportTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_timings_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _dbPath = _workspaceService.GetDatabasePath();

        new RecitationCatalogService(_workspaceService).InitializeAsync().GetAwaiter().GetResult();

        using var db = new AppDbContext(_dbPath);
        db.Surahs.Add(new SurahEntity { Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 7, StartPage = 1, EndPage = 1 });
        for (int a = 1; a <= 7; a++)
            db.Ayahs.Add(new AyahEntity { Id = $"1:{a}", SurahNumber = 1, AyahNumber = a, VerseNo = a, PageNumber = 1 });
        db.AudioSources.Add(new AudioSourceEntity
        {
            Id = SourceId,
            CollectionId = Collection,
            SurahNumber = 1,
            RelativeFilePath = "media/audio/husary_murattal_warsh/001.mp3",
            SourceUrl = "https://cdn.mp3quran.net/audio/mahmoud-husary/r3/001.mp3",
            DurationSeconds = 58.3575
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

    private AyahTimingImportService Service() => new(_workspaceService, new HttpClient(_handler));

    private List<AyahPassageEntity> Passages()
    {
        using var db = new AppDbContext(_dbPath);
        return db.Passages.Where(p => p.AudioSourceId == SourceId).OrderBy(p => p.AyahNumber).ToList();
    }

    private void SetAudioDuration(double seconds)
    {
        using var db = new AppDbContext(_dbPath);
        db.AudioSources.Single(a => a.Id == SourceId).DurationSeconds = seconds;
        db.SaveChanges();
    }

    [Fact]
    public async Task Catalog_HusaryWarsh_HasMp3QuranTimingReadId()
    {
        var service = Service();
        Assert.Equal(RecitationCatalogService.HusaryTimingReadId, await service.GetTimingReadIdAsync(Collection));
        Assert.Null(await service.GetTimingReadIdAsync(RecitationCatalogService.SaberCollection));
    }

    [Fact]
    public async Task Import_CreatesProposedFullPassages_AndReusesLocalCache()
    {
        _handler.Json = FatihaJson;
        var service = Service();

        var result = await service.ImportAsync(Collection, 1);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(7, result.ImportedCount);
        Assert.Null(result.Warning);
        var passages = Passages();
        Assert.Equal(7, passages.Count);
        Assert.All(passages, p =>
        {
            Assert.Equal(PassageKind.Full, p.Kind);
            Assert.True(p.IsProposedTiming);
            Assert.True(p.IsSelectedForExport);
            Assert.Equal("mp3quran:120", p.Notes);
        });
        // Ayah 1 : début ramené à 0 s (isti'adha + basmala incluses), fin publiée conservée
        Assert.Equal(0.0, passages[0].StartTimeSec, 3);
        Assert.Equal(18.72, passages[0].EndTimeSec, 3);
        Assert.Equal(18.72, passages[1].StartTimeSec, 3);
        Assert.Equal(passages[0].EndTimeSec, passages[1].StartTimeSec, 3);
        Assert.Equal("https://www.mp3quran.net/api/v3/ayat_timing?surah=1&read=120", _handler.RequestedUrls.Single());
        Assert.True(File.Exists(service.GetCachePath(120, 1)));

        // Deuxième import : rien n'est réécrit et le cache évite tout appel réseau
        var second = await Service().ImportAsync(Collection, 1);
        Assert.True(second.Success);
        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(7, second.SkippedManualCount);
        Assert.Single(_handler.RequestedUrls);
        Assert.Equal(7, Passages().Count);
    }

    [Fact]
    public async Task Import_NeverTouchesManualPassage()
    {
        using (var db = new AppDbContext(_dbPath))
        {
            db.Passages.Add(new AyahPassageEntity
            {
                AudioSourceId = SourceId, SurahNumber = 1, AyahNumber = 3, AyahId = "1:3",
                StartTimeSec = 24.0, EndTimeSec = 29.0, Kind = PassageKind.Full,
                IsSelectedForExport = true, IsValidatedByUser = true
            });
            db.SaveChanges();
        }
        _handler.Json = FatihaJson;

        var result = await Service().ImportAsync(Collection, 1, replaceProposed: true);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(6, result.ImportedCount);
        Assert.Equal(1, result.SkippedManualCount);
        var ayah3 = Assert.Single(Passages(), p => p.AyahNumber == 3);
        Assert.Equal(24.0, ayah3.StartTimeSec, 3);
        Assert.Equal(29.0, ayah3.EndTimeSec, 3);
        Assert.False(ayah3.IsProposedTiming);
    }

    [Fact]
    public async Task Import_WithOffset_ReplacesOnlyProposedPassages()
    {
        _handler.Json = FatihaJson;
        await Service().ImportAsync(Collection, 1);

        var result = await Service().ImportAsync(Collection, 1, replaceProposed: true, offsetSec: 0.5);

        Assert.True(result.Success, result.ErrorMessage);
        // L'ayah 1 garde son début à 0 s : seule sa fin bouge avec le décalage
        Assert.Equal(7, result.UpdatedCount);
        var passages = Passages();
        Assert.Equal(0.0, passages[0].StartTimeSec, 3);
        Assert.Equal(19.22, passages[0].EndTimeSec, 3);
        Assert.Equal(2, passages[0].Revision);
        Assert.True(passages[0].IsProposedTiming);
        Assert.Equal(19.22, passages[1].StartTimeSec, 3);
        Assert.Equal(24.12, passages[1].EndTimeSec, 3);
    }

    [Fact]
    public async Task Import_WithoutOpeningOption_KeepsPublishedFirstAyahStart()
    {
        _handler.Json = FatihaJson;

        var result = await Service().ImportAsync(Collection, 1, includeOpeningInFirstAyah: false);

        Assert.True(result.Success, result.ErrorMessage);
        var first = Passages()[0];
        Assert.Equal(13.24, first.StartTimeSec, 3);
        Assert.Equal(18.72, first.EndTimeSec, 3);
    }

    [Fact]
    public async Task Import_AyahCountMismatch_IsRefusedWithoutWriting()
    {
        // 6 entrées seulement (ex. numérotation d'une autre riwaya) : refus
        var lastAyah = FatihaJson.IndexOf("{\"ayah\":7", StringComparison.Ordinal);
        _handler.Json = FatihaJson[..lastAyah].TrimEnd().TrimEnd(',') + "]";

        var result = await Service().ImportAsync(Collection, 1);

        Assert.False(result.Success);
        Assert.Contains("عدد الآيات", result.ErrorMessage);
        Assert.Empty(Passages());
    }

    [Theory]
    [InlineData(40.0)]   // le fichier est plus court que les minutages
    [InlineData(300.0)]  // le fichier est beaucoup plus long : autre enregistrement
    public async Task Import_AudioDurationMismatch_IsRefused(double durationSec)
    {
        SetAudioDuration(durationSec);
        _handler.Json = FatihaJson;

        var result = await Service().ImportAsync(Collection, 1);

        Assert.False(result.Success);
        Assert.Contains("ليس نفس تسجيل المصدر", result.ErrorMessage);
        Assert.Empty(Passages());
    }

    [Fact]
    public async Task Import_ReciterWithoutPublishedTimings_IsRefused()
    {
        var result = await Service().ImportAsync(RecitationCatalogService.SaberCollection, 1);

        Assert.False(result.Success);
        Assert.Empty(_handler.RequestedUrls);
    }

    [Fact]
    public async Task Import_LocallyImportedAudio_SucceedsWithListeningWarning()
    {
        using (var db = new AppDbContext(_dbPath))
        {
            db.AudioSources.Single(a => a.Id == SourceId).SourceUrl = string.Empty;
            db.SaveChanges();
        }
        _handler.Json = FatihaJson;

        var result = await Service().ImportAsync(Collection, 1);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task ResavingProposedPassage_MakesItManual()
    {
        _handler.Json = FatihaJson;
        await Service().ImportAsync(Collection, 1);
        var annotation = new AnnotationService(_workspaceService);

        // Comme F8 dans le studio : nouvelle prise principale sur la même ayah
        var (ok, _, saved) = await annotation.SavePassageAsync(new AyahPassageEntity
        {
            AudioSourceId = SourceId, SurahNumber = 1, AyahNumber = 1, AyahId = "1:1",
            StartTimeSec = 13.30, EndTimeSec = 18.70, Kind = PassageKind.Full,
            IsSelectedForExport = true, IsValidatedByUser = true
        });

        Assert.True(ok);
        Assert.False(saved.IsProposedTiming);
        var ayah1 = Assert.Single(Passages(), p => p.AyahNumber == 1);
        Assert.False(ayah1.IsProposedTiming);
        Assert.Equal(13.30, ayah1.StartTimeSec, 3);

        // Un nouvel import ne touche plus à cette ayah
        var again = await Service().ImportAsync(Collection, 1, replaceProposed: true, offsetSec: 1.0);
        Assert.True(again.Success);
        Assert.Equal(13.30, Assert.Single(Passages(), p => p.AyahNumber == 1).StartTimeSec, 3);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string Json { get; set; } = "[]";
        public List<string> RequestedUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json")
            });
        }
    }
}
