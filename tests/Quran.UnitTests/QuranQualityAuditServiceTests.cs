using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.UnitTests;

public class QuranQualityAuditServiceTests
{
    private class FakeStorageService : IStorageService
    {
        private readonly HashSet<string> _existingKeys;
        private readonly string _root;

        public FakeStorageService(IEnumerable<string>? existingKeys = null, string? root = null)
        {
            _existingKeys = existingKeys != null ? new HashSet<string>(existingKeys) : new HashSet<string>();
            _root = root ?? Path.GetTempPath();
        }

        public Task<bool> FileExistsAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
        {
            if (_existingKeys.Count == 0) return Task.FromResult(true); // default true if not configured
            return Task.FromResult(_existingKeys.Contains(relativeStorageKey));
        }

        public Task<Stream?> GetAudioStreamAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(new MemoryStream());

        public string GetPhysicalPath(string relativeStorageKey) => Path.Combine(_root, relativeStorageKey);

        public string GetPublicUrl(string relativeStorageKey) => "/audio/" + relativeStorageKey;

        public Task<string> SaveAudioAsync(Stream content, string relativeStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(relativeStorageKey);
    }

    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new QuranDbContext(options);
    }

    [Fact]
    public async Task AuditSurahConsistency_DetectsGapsBetweenConsecutiveSegments()
    {
        // Arrange
        using var db = CreateInMemoryDbContext("Audit_Gap_Test");
        var storage = new FakeStorageService();

        var surah = new Surah
        {
            Id = 1,
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            RevelationOrder = 5,
            RevelationType = RevelationType.Meccan
        };
        db.Surahs.Add(surah);

        // Segment 1: 0ms to 5000ms
        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "1:1",
            StartMs = 0,
            EndMs = 5000,
            Status = AudioSegmentStatus.Approved
        });

        // Segment 2: 7500ms to 12000ms -> Gap of 2500ms!
        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "1:2",
            StartMs = 7500,
            EndMs = 12000,
            Status = AudioSegmentStatus.Approved
        });

        await db.SaveChangesAsync();

        var auditService = new QuranQualityAuditService(db, storage, NullLogger<QuranQualityAuditService>.Instance);

        // Act
        var anomalies = await auditService.AuditSurahConsistencyAsync(1);

        // Assert
        var gapAnomaly = anomalies.FirstOrDefault(a => a.Type == QualityAnomalyType.Gap);
        Assert.NotNull(gapAnomaly);
        Assert.Equal(QualityAnomalySeverity.Info, gapAnomaly.Severity);
        Assert.Contains("2.50 ثانية", gapAnomaly.Description);
    }

    [Fact]
    public async Task AuditSurahConsistency_DetectsOverlapBetweenConsecutiveSegments()
    {
        // Arrange
        using var db = CreateInMemoryDbContext("Audit_Overlap_Test");
        var storage = new FakeStorageService();

        var surah = new Surah
        {
            Id = 2,
            Number = 2,
            NameArabic = "البقرة",
            NameLatin = "Al-Baqarah",
            AyahCount = 286,
            RevelationOrder = 87,
            RevelationType = RevelationType.Medinan
        };
        db.Surahs.Add(surah);

        // Segment 1: 1000ms to 6000ms
        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "2:1",
            StartMs = 1000,
            EndMs = 6000,
            Status = AudioSegmentStatus.Draft
        });

        // Segment 2: 5500ms to 10000ms -> Overlap of 500ms!
        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "2:2",
            StartMs = 5500,
            EndMs = 10000,
            Status = AudioSegmentStatus.Draft
        });

        await db.SaveChangesAsync();

        var auditService = new QuranQualityAuditService(db, storage, NullLogger<QuranQualityAuditService>.Instance);

        // Act
        var anomalies = await auditService.AuditSurahConsistencyAsync(2);

        // Assert
        var overlapAnomaly = anomalies.FirstOrDefault(a => a.Type == QualityAnomalyType.Overlap);
        Assert.NotNull(overlapAnomaly);
        Assert.Equal(QualityAnomalySeverity.Warning, overlapAnomaly.Severity);
        Assert.Contains("500 ms", overlapAnomaly.Description);
    }

    [Fact]
    public async Task AuditSurahConsistency_DetectsMissingMediaFile_WhenPublishedAssetAbsentOnDisk()
    {
        // Arrange
        using var db = CreateInMemoryDbContext("Audit_MissingMedia_Test");
        // Only sources/001/original.mp3 exists, published cut file does NOT exist
        var storage = new FakeStorageService(existingKeys: new[] { "sources/001/original.mp3" });

        var surah = new Surah
        {
            Id = 1,
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            RevelationOrder = 5,
            RevelationType = RevelationType.Meccan
        };
        db.Surahs.Add(surah);

        var asset1 = new AudioAsset { Id = 10, StorageKey = "published/001/001/test.mp3", OriginalFileName = "test.mp3", MimeType = "audio/mpeg" };
        db.AudioAssets.Add(asset1);

        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "1:1",
            StartMs = 0,
            EndMs = 5000,
            Status = AudioSegmentStatus.Published,
            PublishedAudioAssetId = 10,
            PublishedAudioAsset = asset1
        });

        await db.SaveChangesAsync();

        var auditService = new QuranQualityAuditService(db, storage, NullLogger<QuranQualityAuditService>.Instance);

        // Act
        var anomalies = await auditService.AuditSurahConsistencyAsync(1);

        // Assert
        var missingMedia = anomalies.FirstOrDefault(a => a.Type == QualityAnomalyType.MissingMediaFile);
        Assert.NotNull(missingMedia);
        Assert.Equal(QualityAnomalySeverity.Error, missingMedia.Severity);
        Assert.Contains("الملف الصوتي المقتطع مفقود على القرص", missingMedia.Description);
    }

    [Fact]
    public async Task GenerateGlobalQualityReport_CalculatesCoverageAndTotalsAccurately()
    {
        // Arrange
        using var db = CreateInMemoryDbContext("Audit_Global_Test");
        var storage = new FakeStorageService();

        var surah1 = new Surah
        {
            Id = 1,
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            RevelationOrder = 5,
            RevelationType = RevelationType.Meccan
        };
        db.Surahs.Add(surah1);

        var ayah1 = new Ayah { Id = 101, SurahId = 1, AyahNumber = 1, NumberInQuran = 1, TextWarsh = "بِسْمِ اللَّهِ" };
        db.Ayahs.Add(ayah1);

        // Recitation published for Ayah 1
        db.Recitations.Add(new Recitation
        {
            Id = 1,
            AyahId = 101,
            ReciterId = 1,
            RiwayaId = 1,
            AudioAssetId = 1,
            Status = RecitationStatus.Published,
            IsPublished = true
        });

        var asset2 = new AudioAsset { Id = 20, StorageKey = "published/001/001/test.mp3", OriginalFileName = "test.mp3", MimeType = "audio/mpeg" };
        db.AudioAssets.Add(asset2);

        db.AudioSegments.Add(new AudioSegment
        {
            Id = Guid.NewGuid(),
            Reference = "1:1",
            StartMs = 0,
            EndMs = 4000,
            Status = AudioSegmentStatus.Published,
            PublishedAudioAssetId = 20,
            PublishedAudioAsset = asset2
        });

        await db.SaveChangesAsync();

        var auditService = new QuranQualityAuditService(db, storage, NullLogger<QuranQualityAuditService>.Instance);

        // Act
        var report = await auditService.GenerateGlobalQualityReportAsync();

        // Assert
        Assert.Equal(1, report.TotalSurahs);
        Assert.Equal(7, report.TotalAyahs);
        Assert.Equal(1, report.TotalPublishedAyahs);
        Assert.Equal(1, report.TotalPublishedSegments);
        Assert.Equal(1, report.SurahsWithSourceAudio);
        Assert.True(report.GlobalAyahCoveragePercentage > 14.0); // 1/7 = 14.29%
    }
}
