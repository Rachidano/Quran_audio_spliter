using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Import.Services;

public class QuranSeedService
{
    private readonly QuranDbContext _dbContext;
    private readonly ILogger<QuranSeedService> _logger;

    public QuranSeedService(QuranDbContext dbContext, ILogger<QuranSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedReferenceDataAsync(string surahsJsonPath, CancellationToken cancellationToken = default)
    {
        await SeedSurahsAsync(surahsJsonPath, cancellationToken);
        await SeedReciterAndRiwayaAsync(cancellationToken);
        await SeedStandardAwjuhAsync(cancellationToken);
        await SeedSourceAsync(cancellationToken);
    }

    private async Task SeedSurahsAsync(string jsonPath, CancellationToken cancellationToken)
    {
        if (await _dbContext.Surahs.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Surahs are already seeded. Skipping.");
            return;
        }

        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("Surahs JSON file not found at {Path}", jsonPath);
            return;
        }

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        var surahsData = JsonSerializer.Deserialize<List<SurahSeedModel>>(json);

        if (surahsData == null || surahsData.Count == 0) return;

        foreach (var item in surahsData)
        {
            var revelationType = Enum.TryParse<RevelationType>(item.RevelationType, true, out var rev) 
                ? rev 
                : RevelationType.Meccan;

            _dbContext.Surahs.Add(new Surah
            {
                Number = item.Number,
                NameArabic = item.NameArabic,
                NameLatin = item.NameLatin,
                NameTranslation = item.NameTranslation,
                AyahCount = item.AyahCount,
                RevelationOrder = item.RevelationOrder,
                RevelationType = revelationType,
                IsActive = true
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully seeded {Count} Surahs.", surahsData.Count);
    }

    private async Task SeedReciterAndRiwayaAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Reciters.AnyAsync(r => r.NameLatin == "Saber Abdelhakam", cancellationToken))
        {
            _dbContext.Reciters.Add(new Reciter
            {
                NameArabic = "صابر عبد الحكم",
                NameLatin = "Saber Abdelhakam",
                Description = "القارئ الشيخ صابر عبد الحكم — مصحف رواية ورش عن نافع بجميع الأوجه الشاطبية",
                IsActive = true
            });
        }

        if (!await _dbContext.Riwayas.AnyAsync(r => r.NameLatin.Contains("Warsh"), cancellationToken))
        {
            _dbContext.Riwayas.Add(new Riwaya
            {
                NameArabic = "ورش عن نافع",
                NameLatin = "Warsh 'an Nafi'",
                Description = "رواية الإمام ورش عن نافع المدني من طريق الشاطبية"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedStandardAwjuhAsync(CancellationToken cancellationToken)
    {
        // Retire ambiguous legacy labels. No pending catalog proposal becomes a validated DB wajh.
        await Quran.Infrastructure.Services.AwjuhCatalog.ReplaceLegacyAsync(_dbContext, cancellationToken);
    }

    private async Task SeedSourceAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Sources.AnyAsync(s => s.Name.Contains("Archive.org"), cancellationToken))
        {
            _dbContext.Sources.Add(new Source
            {
                Name = "Internet Archive (z0603_202406x)",
                Url = "https://archive.org/details/z0603_202406x",
                Type = "ArchiveOrg",
                Description = "صابر عبد الحكم مصحف رواية ورش ب جميع الأوجه الشاطبية 114 سورة كامل 128 ك ب"
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private record SurahSeedModel(
        int Number,
        string NameArabic,
        string NameLatin,
        string NameTranslation,
        int AyahCount,
        int RevelationOrder,
        string RevelationType
    );
}
