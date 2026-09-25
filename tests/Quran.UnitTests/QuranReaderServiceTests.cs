using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Import.Services;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class QuranReaderServiceTests
{
    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new QuranDbContext(options);
    }

    [Fact]
    public async Task HistoricalText_IsPreservedButNeverUsedAsReadingFallback()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext("Quran_Test_Fatiha");

        var seedService = new QuranSeedService(dbContext, NullLogger<QuranSeedService>.Instance);
        var importService = new WarshAyahImportService(dbContext, NullLogger<WarshAyahImportService>.Instance);

        var surahsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "reference", "surahs.json");
        var fatihaPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "reference", "warsh_fatiha.json");

        // Act 1: Seeding
        await seedService.SeedReferenceDataAsync(surahsPath);
        var importedAyahs = await importService.ImportAyahsFromJsonAsync(fatihaPath);

        // Assert 1
        Assert.Equal(8, importedAyahs); // Ayah 0 (Basmala) + Ayahs 1 to 7
        var totalSurahs = await dbContext.Surahs.CountAsync();
        Assert.Equal(114, totalSurahs);

        // Act 2: Reading through QuranReaderService
        var reader = new QuranReaderService(dbContext);
        var surahs = await reader.GetAllSurahsAsync();
        var fatiha = await reader.GetSurahByNumberAsync(1);
        var ayahs = await reader.GetAyahsBySurahAsync(1);
        var ayah0 = await reader.GetAyahAsync(1, 0);
        var ayah1 = await reader.GetAyahAsync(1, 1);

        // Assert 2
        Assert.Equal(114, surahs.Count);
        Assert.NotNull(fatiha);
        Assert.Equal("الفاتحة", fatiha.NameArabic);
        Assert.Empty(ayahs);
        Assert.Null(ayah0);
        Assert.Null(ayah1);
        Assert.Empty(await reader.SearchAsync("1:2"));
        Assert.Empty(await reader.SearchAsync("الحمد لله"));
        Assert.Equal(8, await dbContext.Ayahs.CountAsync()); // Preserved, never exposed as fallback.
    }

    [Fact]
    public async Task CanonicalReferences_AreAvailableWithoutUnqualifiedText()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        db.Surahs.Add(new Quran.Domain.Entities.Surah { Number = 2, NameArabic = "test", NameLatin = "test", AyahCount = 286 });
        var canonical = new Quran.Domain.Entities.QuranCorpus { Code = "marw-newMushaf", NameArabic = "test", CountingSystem = "test", Provenance = "fixture" };
        var candidate = new Quran.Domain.Entities.QuranCorpus { Code = "qpc-warsh-v10-candidate", NameArabic = "test", CountingSystem = "test", Provenance = "fixture" };
        for (var n = 1; n <= 285; n++) db.CorpusAyahs.Add(new Quran.Domain.Entities.CorpusAyah { Corpus = canonical, SurahNumber = 2, AyahNumber = n, TextArabic = "placeholder", SearchText = "placeholder" });
        db.CorpusAyahs.Add(new Quran.Domain.Entities.CorpusAyah { Corpus = candidate, SurahNumber = 2, AyahNumber = 1, TextArabic = "UNQUALIFIED", SearchText = "UNQUALIFIED" });
        await db.SaveChangesAsync();
        var reader = new QuranReaderService(db);
        var ayahs = await reader.GetAyahsBySurahAsync(2);
        Assert.Equal(285, ayahs.Count);
        Assert.All(ayahs, a => Assert.Empty(a.TextWarsh));
        Assert.Single(await reader.SearchAsync("2:4"));
        Assert.Empty(await reader.SearchAsync("UNQUALIFIED"));
        Assert.Empty(await reader.SearchAsync("2:286"));
        Assert.Empty((await reader.SearchAsync("2:4")).Single().TextWarsh);
        Assert.Equal(286, await db.CorpusAyahs.CountAsync());
    }
}
