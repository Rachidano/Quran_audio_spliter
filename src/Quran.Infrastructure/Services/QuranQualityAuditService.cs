using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class QuranQualityAuditService : IQuranQualityAuditService
{
    private readonly QuranDbContext _db;
    private readonly IStorageService _storage;
    private readonly ILogger<QuranQualityAuditService> _logger;

    public QuranQualityAuditService(
        QuranDbContext db,
        IStorageService storage,
        ILogger<QuranQualityAuditService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<GlobalQualityReportDto> GenerateGlobalQualityReportAsync(CancellationToken cancellationToken = default)
    {
        var surahs = await _db.Surahs
            .OrderBy(s => s.Number)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allAnomalies = new List<QualityAnomalyDto>();
        var surahCoverages = new List<SurahQualityCoverageDto>();

        int totalAyahs = 0;
        int totalCoveredAyahs = 0;
        int totalPublishedAyahs = 0;
        int surahsWithSourceAudio = 0;
        int totalSegments = 0;
        int totalPublishedSegments = 0;

        foreach (var surah in surahs)
        {
            var surahAnomalies = await AuditSurahConsistencyAsync(surah.Number, cancellationToken);
            allAnomalies.AddRange(surahAnomalies);

            var references = await CanonicalAudioQueries.References(_db)
                .Where(a => a.SurahNumber == surah.Number)
                .Select(a => a.SurahNumber + ":" + a.AyahNumber).ToListAsync(cancellationToken);
            var referenceSet = references.ToHashSet();
            var surahPrefix = $"{surah.Number}:";
            var segments = await CanonicalAudioQueries.Segments(_db)
                .Where(s => s.Reference != null && s.Reference.StartsWith(surahPrefix))
                .AsNoTracking().ToListAsync(cancellationToken);

            var sourceKey = $"sources/{surah.Number:D3}/original.mp3";
            var sourceExists = await _storage.FileExistsAsync(sourceKey, cancellationToken);
            long sourceSizeBytes = 0;
            double sourceDurationSec = 0;

            if (sourceExists)
            {
                surahsWithSourceAudio++;
                var phys = _storage.GetPhysicalPath(sourceKey);
                if (File.Exists(phys))
                {
                    var fi = new FileInfo(phys);
                    sourceSizeBytes = fi.Length;
                }
            }

            var distinctAyahRefsWithAudio = segments.Where(s => s.SegmentType == AudioSegmentType.FullAyah
                && s.Reference != null && referenceSet.Contains(s.Reference))
                .Select(s => s.Reference).Distinct().Count();
            var publishedReferences = await CanonicalAudioQueries.FullAyahReferences(_db)
                .Where(r => r != null && r.StartsWith(surahPrefix)).ToListAsync(cancellationToken);
            var publishedAyahsCount = publishedReferences.Count(r => r != null && referenceSet.Contains(r));

            var draftCount = segments.Count(s => s.Status == AudioSegmentStatus.Draft);
            var reviewedCount = segments.Count(s => s.Status == AudioSegmentStatus.Reviewed);
            var approvedCount = segments.Count(s => s.Status == AudioSegmentStatus.Approved);
            var publishedSegCount = segments.Count(s => s.Status == AudioSegmentStatus.Published);

            totalAyahs += references.Count;
            totalCoveredAyahs += distinctAyahRefsWithAudio;
            totalPublishedAyahs += publishedAyahsCount;
            totalSegments += segments.Count;
            totalPublishedSegments += publishedSegCount;

            var coveragePct = references.Count > 0 ? (double)publishedAyahsCount / references.Count * 100.0 : 0.0;

            surahCoverages.Add(new SurahQualityCoverageDto(
                SurahNumber: surah.Number,
                SurahNameArabic: surah.NameArabic,
                SurahNameLatin: surah.NameLatin,
                TotalAyahs: references.Count,
                CoveredAyahs: distinctAyahRefsWithAudio,
                PublishedAyahs: publishedAyahsCount,
                DraftSegments: draftCount,
                ReviewedSegments: reviewedCount,
                ApprovedSegments: approvedCount,
                PublishedSegments: publishedSegCount,
                HasSourceAudio: sourceExists,
                SourceSizeBytes: sourceSizeBytes,
                SourceDurationSeconds: sourceDurationSec,
                AnomalyCount: surahAnomalies.Count,
                CoveragePercentage: Math.Round(coveragePct, 1)
            ));
        }

        var globalCoveragePct = totalAyahs > 0 ? (double)totalPublishedAyahs / totalAyahs * 100.0 : 0.0;

        return new GlobalQualityReportDto(
            TotalSurahs: surahs.Count,
            TotalAyahs: totalAyahs,
            TotalCoveredAyahs: totalCoveredAyahs,
            TotalPublishedAyahs: totalPublishedAyahs,
            GlobalAyahCoveragePercentage: Math.Round(globalCoveragePct, 2),
            SurahsWithSourceAudio: surahsWithSourceAudio,
            TotalSegments: totalSegments,
            TotalPublishedSegments: totalPublishedSegments,
            TotalGaps: allAnomalies.Count(a => a.Type == QualityAnomalyType.Gap),
            TotalOverlaps: allAnomalies.Count(a => a.Type == QualityAnomalyType.Overlap),
            TotalMissingMedia: allAnomalies.Count(a => a.Type == QualityAnomalyType.MissingMediaFile),
            TotalUnmappedReferences: allAnomalies.Count(a => a.Type == QualityAnomalyType.UnmappedReference),
            Anomalies: allAnomalies,
            SurahCoverages: surahCoverages
        );
    }

    public async Task<IReadOnlyList<QualityAnomalyDto>> AuditSurahConsistencyAsync(int surahNumber, CancellationToken cancellationToken = default)
    {
        var anomalies = new List<QualityAnomalyDto>();

        var surah = await _db.Surahs
            .FirstOrDefaultAsync(s => s.Number == surahNumber, cancellationToken);

        if (surah == null)
        {
            anomalies.Add(new QualityAnomalyDto(
                Severity: QualityAnomalySeverity.Error,
                Type: QualityAnomalyType.UnmappedReference,
                SurahNumber: surahNumber,
                AyahReference: null,
                Description: $"السورة رقم {surahNumber} غير معرفة في قاعدة البيانات",
                Details: "بيانات السورة مفقودة في جدول Surahs.",
                SuggestedActionUrl: "/Admin/Inventory",
                ActionLabel: "تهيئة السور"
            ));
            return anomalies;
        }

        // 1. Contrôle du fichier source MP3
        var sourceKey = $"sources/{surahNumber:D3}/original.mp3";
        var sourceExists = await _storage.FileExistsAsync(sourceKey, cancellationToken);
        if (!sourceExists)
        {
            anomalies.Add(new QualityAnomalyDto(
                Severity: QualityAnomalySeverity.Warning,
                Type: QualityAnomalyType.MissingMediaFile,
                SurahNumber: surahNumber,
                AyahReference: null,
                Description: $"ملف التسجيل الصوتي الأصلي لسورة {surah.NameArabic} غير متوفر محلياً",
                Details: $"المسار المطلوب: {sourceKey}",
                SuggestedActionUrl: $"/Admin/Inventory",
                ActionLabel: "تنزيل من الأرشيف"
            ));
        }

        // 2. Contrôle des segments audio
        var surahPrefix = $"{surahNumber}:";
        var segments = await CanonicalAudioQueries.Segments(_db)
            .Include(s => s.SourceAudio).ThenInclude(source => source.Surah)
            .Include(s => s.PublishedAudioAsset)
            .Where(s => s.SourceAudio != null ? s.SourceAudio.Surah.Number == surahNumber : s.Reference != null && s.Reference.StartsWith(surahPrefix))
            .OrderBy(s => s.SourceAudioId).ThenBy(s => s.StartMs)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var references = (await CanonicalAudioQueries.References(_db)
            .Where(a => a.SurahNumber == surahNumber)
            .Select(a => a.SurahNumber + ":" + a.AyahNumber).ToListAsync(cancellationToken)).ToHashSet();

        AudioSegment? previousSegment = null;
        foreach (var seg in segments)
        {
            if (seg.Reference == null || !references.Contains(seg.Reference))
                anomalies.Add(new QualityAnomalyDto(QualityAnomalySeverity.Error, QualityAnomalyType.UnmappedReference,
                    surahNumber, seg.Reference, "مرجع المقطع غير موجود في مصحف MARW لهذه السورة",
                    $"المقطع: {seg.Id}", $"/Admin/AudioEditor/{surahNumber}?segmentId={seg.Id}", "مراجعة المقطع"));

            // Vérification de la validité intrinsèque des bornes
            if (seg.StartMs < 0 || seg.EndMs <= seg.StartMs)
            {
                anomalies.Add(new QualityAnomalyDto(
                    Severity: QualityAnomalySeverity.Error,
                    Type: QualityAnomalyType.Overlap,
                    SurahNumber: surahNumber,
                    AyahReference: seg.Reference,
                    Description: $"توقيتات المقطع غير صالحة: Start={seg.StartMs}ms, End={seg.EndMs}ms",
                    Details: $"المقطع المعرف بالرمز {seg.Id}",
                    SuggestedActionUrl: $"/Admin/AudioEditor/{surahNumber}?segmentId={seg.Id}",
                    ActionLabel: "تصحيح في المحرر"
                ));
            }

            // Contrôle des trous et chevauchements temporels
            if (previousSegment != null && previousSegment.SourceAudioId == seg.SourceAudioId)
            {
                var diff = seg.StartMs - previousSegment.EndMs;
                if (diff > 500)
                {
                    // Intervalle non annoté (trou explicite)
                    var secStr = (diff / 1000.0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    anomalies.Add(new QualityAnomalyDto(
                        Severity: QualityAnomalySeverity.Info,
                        Type: QualityAnomalyType.Gap,
                        SurahNumber: surahNumber,
                        AyahReference: seg.Reference,
                        Description: $"فاصل زمني غير مسجل بين الآيات: {secStr} ثانية",
                        Details: $"بين المقطع السابق (نهاية {previousSegment.EndMs}ms) والمقطع الحالي (بداية {seg.StartMs}ms)",
                        SuggestedActionUrl: $"/Admin/AudioEditor/{surahNumber}?timeMs={previousSegment.EndMs}",
                        ActionLabel: "فحص في المحرر"
                    ));
                }
                else if (diff < -50)
                {
                    // Chevauchement temporel
                    anomalies.Add(new QualityAnomalyDto(
                        Severity: QualityAnomalySeverity.Warning,
                        Type: QualityAnomalyType.Overlap,
                        SurahNumber: surahNumber,
                        AyahReference: seg.Reference,
                        Description: $"تداخل زمني بين مقطعين متتاليين: {(-diff)} ms",
                        Details: $"المقطع السابق ينتهي عند {previousSegment.EndMs}ms والمقطع الحالي يبدأ عند {seg.StartMs}ms",
                        SuggestedActionUrl: $"/Admin/AudioEditor/{surahNumber}?segmentId={seg.Id}",
                        ActionLabel: "تدقيق التداخل"
                    ));
                }
            }

            // Contrôle d'existence physique des assets générés ou publiés
            if (seg.Status == AudioSegmentStatus.Published || seg.Status == AudioSegmentStatus.Rendered)
            {
                var storageKey = seg.PublishedAudioAsset?.StorageKey;
                {
                    var assetExists = !string.IsNullOrEmpty(storageKey) && await _storage.FileExistsAsync(storageKey, cancellationToken);
                    if (!assetExists)
                    {
                        anomalies.Add(new QualityAnomalyDto(
                            Severity: QualityAnomalySeverity.Error,
                            Type: QualityAnomalyType.MissingMediaFile,
                            SurahNumber: surahNumber,
                            AyahReference: seg.Reference,
                            Description: $"الملف الصوتي المقتطع مفقود على القرص رغم تسجيل حالته كـ {seg.Status}",
                            Details: $"المسار المسجل: {storageKey}",
                            SuggestedActionUrl: $"/Admin/ReviewQueue?SurahNumber={surahNumber}",
                            ActionLabel: "إعادة التصيير"
                        ));
                    }
                }
            }

            // Keep the furthest covered boundary: nested portions must not invent a gap.
            if (seg.StartMs >= 0 && seg.EndMs > seg.StartMs &&
                (previousSegment == null || previousSegment.SourceAudioId != seg.SourceAudioId || seg.EndMs > previousSegment.EndMs))
                previousSegment = seg;
        }

        return anomalies;
    }
}
