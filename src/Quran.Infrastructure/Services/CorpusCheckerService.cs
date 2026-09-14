using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class CorpusCheckerService : ICorpusCheckerService
{
    private readonly QuranDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly IAudioProcessorService _audioProcessor;
    private readonly ILogger<CorpusCheckerService> _logger;

    public CorpusCheckerService(
        QuranDbContext dbContext,
        IStorageService storageService,
        IAudioProcessorService audioProcessor,
        ILogger<CorpusCheckerService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _audioProcessor = audioProcessor;
        _logger = logger;
    }

    public async Task<CorpusCheckReport> CheckCorpusAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        // 1. Contrôle des sourates
        var surahCount = await _dbContext.Surahs.CountAsync(cancellationToken);
        if (surahCount < 114)
        {
            issues.Add($"Nombre de sourates incomplet : {surahCount}/114.");
        }

        // 2. Contrôle des ayat
        var totalAyahs = await _dbContext.Ayahs.CountAsync(cancellationToken);
        var ayahsWithoutText = await _dbContext.Ayahs
            .Where(a => string.IsNullOrWhiteSpace(a.TextWarsh))
            .CountAsync(cancellationToken);

        if (ayahsWithoutText > 0)
        {
            issues.Add($"{ayahsWithoutText} ayat n'ont pas de texte Warsh renseigné.");
        }

        var ayahsWithoutAudio = await _dbContext.Ayahs
            .Where(a => !a.Recitations.Any(r => r.IsPublished && r.Status == RecitationStatus.Published))
            .CountAsync(cancellationToken);

        // 3. Audio sans Wajh
        var audioWithoutWajh = await _dbContext.Recitations
            .Where(r => r.WajhId == null)
            .CountAsync(cancellationToken);

        if (audioWithoutWajh > 0)
        {
            issues.Add($"{audioWithoutWajh} récitations n'ont pas de Wajh spécifié.");
        }

        // 4. Intégrité des fichiers physiques et Checksums
        var audioAssets = await _dbContext.AudioAssets.AsNoTracking().ToListAsync(cancellationToken);
        int missingFilesCount = 0;
        int checksumMismatchCount = 0;

        foreach (var asset in audioAssets)
        {
            var physicalPath = _storageService.GetPhysicalPath(asset.StorageKey);
            if (!File.Exists(physicalPath))
            {
                missingFilesCount++;
                issues.Add($"Fichier audio manquant sur disque : {asset.StorageKey}");
                continue;
            }

            if (!string.IsNullOrEmpty(asset.Checksum))
            {
                var actualChecksum = _audioProcessor.ComputeChecksum(physicalPath);
                if (!string.Equals(actualChecksum, asset.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    checksumMismatchCount++;
                    issues.Add($"Checksum invalide pour {asset.StorageKey} (attendu: {asset.Checksum}, actuel: {actualChecksum})");
                }
            }
        }

        // 5. Contenus publiés sans validation formelle
        var unvalidatedPublished = await _dbContext.Recitations
            .Where(r => r.IsPublished && r.Status != RecitationStatus.Published)
            .CountAsync(cancellationToken);

        if (unvalidatedPublished > 0)
        {
            issues.Add($"{unvalidatedPublished} récitations sont marquées 'IsPublished' sans avoir le statut PUBLISHED.");
        }

        bool isHealthy = issues.Count == 0 || (issues.Count == 1 && ayahsWithoutAudio > 0 && surahCount == 114);

        _logger.LogInformation("CheckCorpus finished. Issues count: {Count}, Healthy: {Healthy}", issues.Count, isHealthy);

        return new CorpusCheckReport(
            SurahCount: surahCount,
            ExpectedSurahCount: 114,
            TotalAyahs: totalAyahs,
            AyahsWithoutText: ayahsWithoutText,
            AyahsWithoutAudio: ayahsWithoutAudio,
            AudioWithoutWajh: audioWithoutWajh,
            MissingFilesCount: missingFilesCount,
            ChecksumMismatchCount: checksumMismatchCount,
            UnvalidatedPublishedCount: unvalidatedPublished,
            Issues: issues,
            IsCorpusHealthy: isHealthy
        );
    }
}
