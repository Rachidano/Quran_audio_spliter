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
    public async Task SeedAndReadAlFatiha_ReturnsSevenAyahsAndHandlesSearch()
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
        Assert.Equal(8, ayahs.Count);
        Assert.NotNull(ayah0);
        Assert.Equal("بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ", ayah0.TextWarsh);
        Assert.NotNull(ayah1);
        Assert.Equal("ٱلۡحَمۡدُ لِلَّهِ رَبِّ ٱلۡعَٰلَمِينَ", ayah1.TextWarsh);

        // Act 3: Search by reference "1:2"
        var refSearchResults = await reader.SearchAsync("1:2");
        Assert.Single(refSearchResults);
        Assert.Equal(2, refSearchResults.First().AyahNumber);

        // Act 4: Search by normalized text "الحمد لله"
        var textSearchResults = await reader.SearchAsync("الحمد لله");
        Assert.NotEmpty(textSearchResults);
        Assert.Contains(textSearchResults, r => r.AyahNumber == 1);
    }
}
