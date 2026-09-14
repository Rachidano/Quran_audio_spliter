using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.Common;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;

namespace Quran.Import.Services;

public class WarshAyahImportService
{
    private readonly QuranDbContext _dbContext;
    private readonly ILogger<WarshAyahImportService> _logger;

    public WarshAyahImportService(QuranDbContext dbContext, ILogger<WarshAyahImportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> ImportAyahsFromJsonAsync(string jsonFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jsonFilePath))
        {
            _logger.LogError("Warsh Ayahs JSON file not found at {Path}", jsonFilePath);
            throw new FileNotFoundException("Le fichier spécifié n'existe pas.", jsonFilePath);
        }

        var json = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
        var ayahsData = JsonSerializer.Deserialize<List<AyahImportModel>>(json);

        if (ayahsData == null || ayahsData.Count == 0)
        {
            _logger.LogWarning("No Ayah data found in {Path}", jsonFilePath);
            return 0;
        }

        int importedCount = 0;

        foreach (var item in ayahsData)
        {
            var surah = await _dbContext.Surahs.FirstOrDefaultAsync(s => s.Number == item.SurahNumber, cancellationToken);
            if (surah == null)
            {
                _logger.LogWarning("Surah #{SurahNumber} not found. Skipping Ayah #{AyahNumber}", item.SurahNumber, item.AyahNumber);
                continue;
            }

            var normalizedSearchText = ArabicTextNormalizer.Normalize(item.TextWarsh);

            var existingAyah = await _dbContext.Ayahs
                .FirstOrDefaultAsync(a => a.SurahId == surah.Id && a.AyahNumber == item.AyahNumber, cancellationToken);

            if (existingAyah != null)
            {
                // Mise à jour si nécessaire
                existingAyah.TextWarsh = item.TextWarsh;
                existingAyah.SearchText = normalizedSearchText;
                existingAyah.NumberInQuran = item.NumberInQuran;
                existingAyah.PageNumber = item.PageNumber;
                existingAyah.LineStart = item.LineStart;
                existingAyah.LineEnd = item.LineEnd;
            }
            else
            {
                _dbContext.Ayahs.Add(new Ayah
                {
                    SurahId = surah.Id,
                    AyahNumber = item.AyahNumber,
                    NumberInQuran = item.NumberInQuran,
                    TextWarsh = item.TextWarsh,
                    SearchText = normalizedSearchText,
                    PageNumber = item.PageNumber,
                    LineStart = item.LineStart,
                    LineEnd = item.LineEnd
                });
            }

            importedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully imported/updated {Count} Warsh ayat.", importedCount);
        return importedCount;
    }

    private record AyahImportModel(
        int SurahNumber,
        int AyahNumber,
        int NumberInQuran,
        int PageNumber,
        int LineStart,
        int LineEnd,
        string TextWarsh
    );
}
