using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.IntegrationTests;

public class MushafImportIntegrationTests
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING") 
        ?? "Host=localhost;Port=5432;Database=quran_db_test;Username=admin;Password=admin;";

    [Fact(Skip = "Ancien import incorrect : écrit dans quran_db et invente les régions. Remplacé par WarshPilotCorpusTests sur un paquet isolé.")]
    public async Task ImportPages2To5_AndSeedBaqarahPage2()
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        using var db = new QuranDbContext(options);

        // Vérifier si la base est joignable
        if (!await db.Database.CanConnectAsync())
        {
            return; // Skip si pas de DB
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = "d:/Projets/ITComp/Quran/data/audio"
            })
            .Build();

        var service = new MushafImageService(db, config, NullLogger<MushafImageService>.Instance);

        // Importer les pages 2 à 5
        var imported = await service.EnsurePagesImportedAsync(2, 5);
        Assert.True(imported >= 0);

        // Récupérer la page 2
        var page2 = await service.GetPageAsync(2);
        Assert.NotNull(page2);

        // Récupérer les 5 premières ayat d'Al-Baqarah
        var baqarah = await db.Surahs.FirstOrDefaultAsync(s => s.Number == 2);
        if (baqarah != null)
        {
            var ayahs = await db.Ayahs
                .Where(a => a.SurahId == baqarah.Id && a.AyahNumber >= 0 && a.AyahNumber <= 5)
                .OrderBy(a => a.AyahNumber)
                .ToListAsync();

            // Vérifier si les régions existent déjà
            var existingCount = await db.AyahImageRegions.CountAsync(r => r.MushafPageId == page2.Id);
            if (existingCount == 0 && ayahs.Count > 0)
            {
                // Ayah 0: Basmala (si présente)
                var ayah0 = ayahs.FirstOrDefault(a => a.AyahNumber == 0);
                if (ayah0 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah0.Id, page2.Id, 0.22m, 0.250m, 0.56m, 0.060m, 1, 1));
                }

                // Ayah 1: الم
                var ayah1 = ayahs.FirstOrDefault(a => a.AyahNumber == 1);
                if (ayah1 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah1.Id, page2.Id, 0.58m, 0.335m, 0.22m, 0.055m, 1, 1));
                }

                // Ayah 2: ذَلِكَ الْكِتَابُ لَا رَيْبَ فِيهِ هُدًى لِلْمُتَّقِينَ
                var ayah2 = ayahs.FirstOrDefault(a => a.AyahNumber == 2);
                if (ayah2 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah2.Id, page2.Id, 0.18m, 0.335m, 0.40m, 0.055m, 1, 1));
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah2.Id, page2.Id, 0.20m, 0.395m, 0.60m, 0.055m, 2, 1));
                }

                // Ayah 3: الَّذِينَ يُؤْمِنُونَ بِالْغَيْبِ وَيُقِيمُونَ الصَّلَاةَ وَمِمَّا رَزَقْنَاهُمْ يُنْفِقُونَ
                var ayah3 = ayahs.FirstOrDefault(a => a.AyahNumber == 3);
                if (ayah3 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah3.Id, page2.Id, 0.20m, 0.460m, 0.60m, 0.055m, 1, 1));
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah3.Id, page2.Id, 0.20m, 0.520m, 0.60m, 0.055m, 2, 1));
                }

                // Ayah 4: وَالَّذِينَ يُؤْمِنُونَ بِمَا أُنْزِلَ إِلَيْكَ وَمَا أُنْزِلَ مِنْ قَبْلِكَ وَبِالْآخِرَةِ هُمْ يُوقِنُونَ
                var ayah4 = ayahs.FirstOrDefault(a => a.AyahNumber == 4);
                if (ayah4 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah4.Id, page2.Id, 0.20m, 0.590m, 0.60m, 0.055m, 1, 1));
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah4.Id, page2.Id, 0.20m, 0.655m, 0.60m, 0.055m, 2, 1));
                }

                // Ayah 5: أُولَئِكَ عَلَى هُدًى مِنْ رَبِّهِمْ وَأُولَئِكَ هُمُ الْمُفْلِحُونَ
                var ayah5 = ayahs.FirstOrDefault(a => a.AyahNumber == 5);
                if (ayah5 != null)
                {
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah5.Id, page2.Id, 0.20m, 0.725m, 0.60m, 0.055m, 1, 1));
                    await service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah5.Id, page2.Id, 0.20m, 0.790m, 0.60m, 0.055m, 2, 1));
                }
            }
        }
    }
}
