using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.AudioProcessor.Services;
using Quran.Import.Services;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class AudioIngestionIntegrationTests
{
    private readonly string _sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "audio", "sources", "001", "original.mp3");

    [Fact]
    public async Task ProcessAndPublishSegments_CreatesPublishedAudioAssetsAndRecitations()
    {
        if (!File.Exists(_sourcePath)) return;

        // Arrange Db
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: "Quran_AudioIngestion_Test")
            .Options;

        using var dbContext = new QuranDbContext(options);

        var seedService = new QuranSeedService(dbContext, NullLogger<QuranSeedService>.Instance);
        var ayahImportService = new WarshAyahImportService(dbContext, NullLogger<WarshAyahImportService>.Instance);

        var surahsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "reference", "surahs.json");
        var fatihaPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "reference", "warsh_fatiha.json");

        await seedService.SeedReferenceDataAsync(surahsPath);
        await ayahImportService.ImportAyahsFromJsonAsync(fatihaPath);

        // Storage & Processor
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), $"quran_test_audio_{Guid.NewGuid():N}")
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var storageService = new LocalStorageService(config, NullLogger<LocalStorageService>.Instance);
        var audioProcessor = new FfmpegAudioProcessor(NullLogger<FfmpegAudioProcessor>.Instance);

        var ingestionService = new AudioIngestionService(dbContext, audioProcessor, storageService, NullLogger<AudioIngestionService>.Instance);

        // Segments de test pour Ayah 1 et 2
        var testSegments = new List<AyahSegmentDefinition>
        {
            new(1, 1, "w01", TimeSpan.FromSeconds(0.6), TimeSpan.FromSeconds(5.8)),
            new(1, 2, "w01", TimeSpan.FromSeconds(7.4), TimeSpan.FromSeconds(5.7))
        };

        // Act
        var publishedCount = await ingestionService.ProcessAndPublishSegmentsAsync(_sourcePath, testSegments);

        // Assert
        Assert.Equal(2, publishedCount);
        
        var readerService = new QuranReaderService(dbContext);
        var ayah1 = await readerService.GetAyahAsync(1, 1);
        Assert.NotNull(ayah1);
        Assert.Single(ayah1.Recitations);
        Assert.True(ayah1.Recitations.First().IsPublished);
        Assert.Contains("001/001/warsh/w01.mp3", ayah1.Recitations.First().AudioUrl);
    }
}
