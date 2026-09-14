using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Import.Services;

public record AyahSegmentDefinition(
    int SurahNumber,
    int AyahNumber,
    string WajhCode,
    TimeSpan StartTime,
    TimeSpan Duration
);

public class AudioIngestionService
{
    private readonly QuranDbContext _dbContext;
    private readonly IAudioProcessorService _audioProcessor;
    private readonly IStorageService _storageService;
    private readonly ILogger<AudioIngestionService> _logger;

    public AudioIngestionService(
        QuranDbContext dbContext,
        IAudioProcessorService audioProcessor,
        IStorageService storageService,
        ILogger<AudioIngestionService> logger)
    {
        _dbContext = dbContext;
        _audioProcessor = audioProcessor;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<int> ProcessAndPublishSegmentsAsync(
        string sourceAudioPath,
        IEnumerable<AyahSegmentDefinition> segments,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceAudioPath))
            throw new FileNotFoundException("Le fichier source audio est introuvable.", sourceAudioPath);

        var reciter = await _dbContext.Reciters.FirstOrDefaultAsync(r => r.NameLatin.Contains("Saber"), cancellationToken)
            ?? throw new InvalidOperationException("Récitateur Saber Abdelhakam non trouvé en base.");

        var riwaya = await _dbContext.Riwayas.FirstOrDefaultAsync(r => r.NameLatin.Contains("Warsh"), cancellationToken)
            ?? throw new InvalidOperationException("Riwaya Warsh non trouvée en base.");

        var source = await _dbContext.Sources.FirstOrDefaultAsync(cancellationToken);

        int publishedCount = 0;

        foreach (var seg in segments)
        {
            var surah = await _dbContext.Surahs.FirstOrDefaultAsync(s => s.Number == seg.SurahNumber, cancellationToken);
            if (surah == null) continue;

            var ayah = await _dbContext.Ayahs.FirstOrDefaultAsync(a => a.SurahId == surah.Id && a.AyahNumber == seg.AyahNumber, cancellationToken);
            if (ayah == null)
            {
                _logger.LogWarning("Ayah {Surah}:{Ayah} introuvable en base pour le rattachement audio.", seg.SurahNumber, seg.AyahNumber);
                continue;
            }

            var wajh = await _dbContext.Awjuh.FirstOrDefaultAsync(w => w.Code == seg.WajhCode, cancellationToken);

            // Convention de nommage : published/{surah:D3}/{ayah:D3}/warsh/{wajhCode}.mp3
            var relativeKey = $"published/{seg.SurahNumber:D3}/{seg.AyahNumber:D3}/warsh/{seg.WajhCode}.mp3";
            var physicalOutputPath = _storageService.GetPhysicalPath(relativeKey);

            // Découpage FFmpeg
            await _audioProcessor.ExtractSegmentAsync(sourceAudioPath, physicalOutputPath, seg.StartTime, seg.Duration, cancellationToken);

            // Analyse du fichier segment extrait
            var probe = await _audioProcessor.ProbeAudioAsync(physicalOutputPath, cancellationToken);

            // Création ou mise à jour de l'AudioAsset
            var audioAsset = await _dbContext.AudioAssets.FirstOrDefaultAsync(a => a.StorageKey == relativeKey, cancellationToken);
            if (audioAsset == null)
            {
                audioAsset = new AudioAsset
                {
                    StorageKey = relativeKey,
                    OriginalFileName = Path.GetFileName(physicalOutputPath),
                    MimeType = probe.MimeType,
                    DurationMs = (long)probe.Duration.TotalMilliseconds,
                    SizeBytes = probe.SizeBytes,
                    Bitrate = probe.Bitrate,
                    Checksum = probe.Checksum,
                    SourceId = source?.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.AudioAssets.Add(audioAsset);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                audioAsset.DurationMs = (long)probe.Duration.TotalMilliseconds;
                audioAsset.SizeBytes = probe.SizeBytes;
                audioAsset.Bitrate = probe.Bitrate;
                audioAsset.Checksum = probe.Checksum;
            }

            // Création ou mise à jour de la Récitation
            var recitation = await _dbContext.Recitations
                .FirstOrDefaultAsync(r => r.AyahId == ayah.Id && r.ReciterId == reciter.Id && r.RiwayaId == riwaya.Id && r.WajhId == (wajh != null ? wajh.Id : null), cancellationToken);

            if (recitation == null)
            {
                recitation = new Recitation
                {
                    AyahId = ayah.Id,
                    ReciterId = reciter.Id,
                    RiwayaId = riwaya.Id,
                    WajhId = wajh?.Id,
                    AudioAssetId = audioAsset.Id,
                    Status = RecitationStatus.Published,
                    IsPublished = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Recitations.Add(recitation);
            }
            else
            {
                recitation.AudioAssetId = audioAsset.Id;
                recitation.Status = RecitationStatus.Published;
                recitation.IsPublished = true;
                recitation.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            publishedCount++;
        }

        _logger.LogInformation("Successfully processed and published {Count} audio segments.", publishedCount);
        return publishedCount;
    }
}
