using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.UnitTests;

public class AudioPeaksServiceTests
{
    private class FakeStorageService : IStorageService
    {
        private readonly string _baseDir;
        public FakeStorageService(string baseDir) => _baseDir = baseDir;

        public Task<bool> FileExistsAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(File.Exists(Path.Combine(_baseDir, relativeStorageKey)));

        public Task<Stream?> GetAudioStreamAsync(string relativeStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public string GetPhysicalPath(string relativeStorageKey)
            => Path.Combine(_baseDir, relativeStorageKey);

        public string GetPublicUrl(string relativeStorageKey)
            => $"/audio/{relativeStorageKey}";

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
    public async Task GetPeaksBySurahAsync_ReturnsUnavailable_WhenNoFileOrDbRecord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "peaks_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            using var db = CreateInMemoryDbContext("Peaks_Missing");
            var storage = new FakeStorageService(tempDir);
            var service = new AudioPeaksService(db, storage, NullLogger<AudioPeaksService>.Instance);

            var result = await service.GetPeaksBySurahAsync(114);

            Assert.NotNull(result);
            Assert.Empty(result.Peaks);
            Assert.Contains("غير متوفر", result.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetPeaksBySurahAsync_LoadsCachedPeaks_WhenCacheExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "peaks_test_" + Guid.NewGuid());
        var cacheDir = Path.Combine(tempDir, "cache", "peaks");
        Directory.CreateDirectory(cacheDir);

        try
        {
            using var db = CreateInMemoryDbContext("Peaks_Cached");
            var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatiha", AyahCount = 7 };
            db.Surahs.Add(surah);

            var source = new SourceAudio
            {
                Id = Guid.NewGuid(),
                SurahId = surah.Id,
                StorageKey = "sources/001/original.mp3",
                ChecksumSha256 = "test-sha256-peaks",
                DurationMs = 60000
            };
            db.SourceAudios.Add(source);
            await db.SaveChangesAsync();

            // Create fake audio file and cached JSON
            var audioFile = Path.Combine(tempDir, "sources", "001", "original.mp3");
            Directory.CreateDirectory(Path.GetDirectoryName(audioFile)!);
            await File.WriteAllBytesAsync(audioFile, new byte[100]);

            var cacheFile = Path.Combine(cacheDir, "test-sha256-peaks.json");
            var json = "{\"sourceSha256\":\"test-sha256-peaks\",\"durationSeconds\":60.0,\"stepSeconds\":0.1,\"peaks\":[0.1,0.2,0.5,0.8,0.3],\"status\":\"Cached\"}";
            await File.WriteAllTextAsync(cacheFile, json);

            var storage = new FakeStorageService(tempDir);
            var service = new AudioPeaksService(db, storage, NullLogger<AudioPeaksService>.Instance, "ffmpeg", cacheDir);

            var result = await service.GetPeaksBySurahAsync(1);

            Assert.NotNull(result);
            Assert.Equal("test-sha256-peaks", result.SourceHash);
            Assert.Equal(5, result.Peaks.Count);
            Assert.Equal(0.1f, result.Peaks[0]);
            Assert.Equal(0.8f, result.Peaks[3]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetPeaksBySurahAsync_SupportsWindowingAndDownsampling()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "peaks_test_" + Guid.NewGuid());
        var cacheDir = Path.Combine(tempDir, "cache", "peaks");
        Directory.CreateDirectory(cacheDir);

        try
        {
            using var db = CreateInMemoryDbContext("Peaks_Windowing");
            var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatiha", AyahCount = 7 };
            db.Surahs.Add(surah);

            var source = new SourceAudio
            {
                Id = Guid.NewGuid(),
                SurahId = surah.Id,
                StorageKey = "sources/001/original.mp3",
                ChecksumSha256 = "test-sha256-window",
                DurationMs = 100000
            };
            db.SourceAudios.Add(source);
            await db.SaveChangesAsync();

            var audioFile = Path.Combine(tempDir, "sources", "001", "original.mp3");
            Directory.CreateDirectory(Path.GetDirectoryName(audioFile)!);
            await File.WriteAllBytesAsync(audioFile, new byte[100]);

            // 100 peaks at step 0.1s = 10s
            var floatArray = Enumerable.Range(0, 100).Select(i => (float)(i / 100.0)).ToArray();
            var cacheFile = Path.Combine(cacheDir, "test-sha256-window.json");
            var rawFile = new
            {
                sourceSha256 = "test-sha256-window",
                durationSeconds = 10.0,
                stepSeconds = 0.1,
                peaks = floatArray,
                status = "Cached"
            };
            await File.WriteAllTextAsync(cacheFile, System.Text.Json.JsonSerializer.Serialize(rawFile));

            var storage = new FakeStorageService(tempDir);
            var service = new AudioPeaksService(db, storage, NullLogger<AudioPeaksService>.Instance, "ffmpeg", cacheDir);

            // Request window: start=2.0s (index 20), duration=4.0s (40 points), downsampled to maxPoints=10
            var result = await service.GetPeaksBySurahAsync(1, startSeconds: 2.0, durationSeconds: 4.0, maxPoints: 10);

            Assert.NotNull(result);
            Assert.Equal(10, result.Peaks.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
