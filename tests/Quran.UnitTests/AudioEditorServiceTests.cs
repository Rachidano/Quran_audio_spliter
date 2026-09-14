using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class AudioEditorServiceTests
{
    private class StubStorageService(string root = "dummy") : IStorageService
    {
        public Task<bool> FileExistsAsync(string relativeStorageKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Stream?> GetAudioStreamAsync(string relativeStorageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(new MemoryStream());
        public string GetPhysicalPath(string relativeStorageKey) => Path.Combine(root, relativeStorageKey);
        public string GetPublicUrl(string relativeStorageKey) => "/audio/" + relativeStorageKey;
        public Task<string> SaveAudioAsync(Stream content, string relativeStorageKey, CancellationToken cancellationToken = default) => Task.FromResult(relativeStorageKey);
    }

    private class StubAudioProcessor : IAudioProcessorService
    {
        public string ComputeChecksum(string filePath) => "abc123sha";
        public Task<IReadOnlyList<TimeSpan>> DetectSilencesAsync(string filePath, double noiseDb = -30, double minDurationSeconds = 0.5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TimeSpan>>(new List<TimeSpan>());
        public Task<string> ExtractSegmentAsync(string sourcePath, string outputPath, TimeSpan startTime, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(outputPath);
        public Task<AudioProbeResult> ProbeAudioAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new AudioProbeResult(TimeSpan.FromMinutes(10), 10_000_000, 192, "audio/mpeg", "abc123sha"));
    }

    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new QuranDbContext(options);
    }

    [Fact]
    public async Task PublishingRepetitions_PreservesEveryAssetAndCanonicalAyah()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var directory = Path.Combine(Path.GetTempPath(), "QuranAudioEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var storage = new StubStorageService(directory);
        var surah = new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Al-Baqarah" };
        var ayah = new Ayah { Id = 24, SurahId = 2, AyahNumber = 4, TextWarsh = "fixture", SearchText = "fixture" };
        var source = new SourceAudio { SurahId = 2, StorageKey = "original.mp3", OriginalFileName = "original.mp3", DurationMs = 120000 };
        db.AddRange(surah, ayah, source);
        var segments = new List<AudioSegment>();
        foreach (var (kind, content) in new[] { (AudioSegmentType.FullAyah, "initial"), (AudioSegmentType.Repetition, "repeat A"), (AudioSegmentType.Repetition, "repeat B") })
        {
            var file = Guid.NewGuid().ToString("N") + ".mp3";
            File.WriteAllText(storage.GetPhysicalPath(file), content);
            var asset = new AudioAsset { StorageKey = file, OriginalFileName = file };
            var segment = new AudioSegment { SourceAudio = source, Ayah = ayah, StartMs = segments.Count * 10000, EndMs = (segments.Count + 1) * 10000,
                SegmentType = kind, Status = AudioSegmentStatus.Rendered, PublishedAudioAsset = asset, SequenceNumber = segments.Count + 1 };
            db.Add(segment); segments.Add(segment);
        }
        await db.SaveChangesAsync();
        var service = new AudioEditorService(db, storage, new StubAudioProcessor(), NullLogger<AudioEditorService>.Instance);
        foreach (var segment in segments) await service.PublishSegmentAsync(segment.Id, "reviewer");
        Assert.Equal(3, segments.Select(s => s.PublishedAudioAsset!.StorageKey).Distinct().Count());
        Assert.Equal(new[] { "initial", "repeat A", "repeat B" }, segments.Select(s => File.ReadAllText(storage.GetPhysicalPath(s.PublishedAudioAsset!.StorageKey))));
        Assert.Equal(segments[0].PublishedAudioAssetId, (await db.Recitations.SingleAsync()).AudioAssetId);
        await service.PublishSegmentAsync(segments[2].Id, "reviewer"); // idempotent
        Assert.Single(await db.Recitations.ToListAsync());
        var edit = segments[2];
        await service.UpdateSegmentAsync(edit.Id, new(edit.AyahId, null, edit.StartMs, edit.EndMs, null, null, AudioSegmentType.PartialAyah, null, Convert.ToBase64String(edit.RowVersion)), "reviewer");
        Assert.Equal(AudioSegmentStatus.Draft, edit.Status);
        Assert.Null(edit.PublishedAudioAssetId);
        Assert.Null(edit.ValidatedAt);
    }

    [Theory]
    [InlineData(AudioSegmentStatus.Draft)]
    [InlineData(AudioSegmentStatus.Reviewed)]
    [InlineData(AudioSegmentStatus.Rejected)]
    public async Task UnapprovedSegment_CannotRenderOrPublish(AudioSegmentStatus status)
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var surah = new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Al-Baqarah" };
        var ayah = new Ayah { Surah = surah, AyahNumber = 4, TextWarsh = "fixture", SearchText = "fixture" };
        var source = new SourceAudio { Surah = surah, StorageKey = "original.mp3", OriginalFileName = "original.mp3" };
        var segment = new AudioSegment { SourceAudio = source, Ayah = ayah, StartMs = 0, EndMs = 1000, Status = status };
        db.Add(segment); await db.SaveChangesAsync();
        var service = new AudioEditorService(db, new StubStorageService(), new StubAudioProcessor(), NullLogger<AudioEditorService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenderSegmentAsync(segment.Id, "reviewer"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishSegmentAsync(segment.Id, "reviewer"));
    }

    [Fact]
    public async Task CreateSegment_ValidatesBounds_AndWordIndices()
    {
        using var db = CreateInMemoryDbContext("Test_CreateSegment_Bounds");
        var surah = new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Al-Baqarah", AyahCount = 286 };
        var ayah = new Ayah { Id = 10, SurahId = 2, AyahNumber = 1, NumberInQuran = 8, TextWarsh = "الٓمٓ", SearchText = "الم" };
        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            SurahId = 2,
            ReciterId = 1,
            RiwayaId = 1,
            OriginalFileName = "original.mp3",
            StorageKey = "sources/002/original.mp3",
            DurationMs = 60000,
            SizeBytes = 1000000,
            Status = SourceAudioStatus.Downloaded
        };
        db.Surahs.Add(surah);
        db.Ayahs.Add(ayah);
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        var service = new AudioEditorService(db, new StubStorageService(), new StubAudioProcessor(), NullLogger<AudioEditorService>.Instance);

        // Negative start
        var badStartReq = new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, -10, 1000, null, null, AudioSegmentType.FullAyah, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSegmentAsync(badStartReq, "admin"));

        // End <= Start
        var badEndReq = new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, 1000, 500, null, null, AudioSegmentType.FullAyah, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSegmentAsync(badEndReq, "admin"));

        // Duration < 200ms
        var tooShortReq = new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, 1000, 1100, null, null, AudioSegmentType.FullAyah, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSegmentAsync(tooShortReq, "admin"));

        // StartWordIndex > EndWordIndex
        var badWordsReq = new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, 1000, 3000, 5, 2, AudioSegmentType.PartialAyah, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSegmentAsync(badWordsReq, "admin"));

        // Valid segment
        var validReq = new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, 1000, 3000, 1, 1, AudioSegmentType.FullAyah, "Test note");
        var created = await service.CreateSegmentAsync(validReq, "admin");

        Assert.NotNull(created);
        Assert.Equal(1000, created.StartMs);
        Assert.Equal(3000, created.EndMs);
        Assert.Equal(AudioSegmentStatus.Draft, created.Status);
    }

    [Fact]
    public async Task UpdateSegment_ConcurrencyCheck_ThrowsOnRowVersionMismatch()
    {
        using var db = CreateInMemoryDbContext("Test_UpdateSegment_Concurrency");
        var surah = new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Al-Baqarah", AyahCount = 286 };
        var ayah = new Ayah { Id = 10, SurahId = 2, AyahNumber = 1, NumberInQuran = 8, TextWarsh = "الٓمٓ", SearchText = "الم" };
        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            SurahId = 2,
            ReciterId = 1,
            RiwayaId = 1,
            OriginalFileName = "original.mp3",
            StorageKey = "sources/002/original.mp3",
            DurationMs = 60000,
            SizeBytes = 1000000,
            Status = SourceAudioStatus.Downloaded
        };
        db.Surahs.Add(surah);
        db.Ayahs.Add(ayah);
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        var service = new AudioEditorService(db, new StubStorageService(), new StubAudioProcessor(), NullLogger<AudioEditorService>.Instance);
        var created = await service.CreateSegmentAsync(
            new CreateAudioSegmentRequest(sourceAudio.Id, ayah.Id, null, 1000, 4000, null, null, AudioSegmentType.FullAyah, null), "admin");

        // Attempt update with wrong RowVersionBase64
        var wrongVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var updateReq = new UpdateAudioSegmentRequest(ayah.Id, null, 1200, 4200, null, null, AudioSegmentType.FullAyah, null, wrongVersion);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateSegmentAsync(created.Id, updateReq, "admin"));

        // Attempt update with matching RowVersionBase64
        var correctUpdateReq = new UpdateAudioSegmentRequest(ayah.Id, null, 1200, 4200, null, null, AudioSegmentType.FullAyah, null, created.RowVersionBase64);
        var updated = await service.UpdateSegmentAsync(created.Id, correctUpdateReq, "admin");

        Assert.Equal(1200, updated.StartMs);
        Assert.Equal(4200, updated.EndMs);
    }

    [Fact]
    public async Task TokenizeAyahsAsync_CreatesWordsWithNormalizedSearch()
    {
        using var db = CreateInMemoryDbContext("Test_TokenizeAyahs");
        var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 1 };
        var ayah = new Ayah
        {
            Id = 1,
            SurahId = 1,
            AyahNumber = 1,
            NumberInQuran = 1,
            TextWarsh = "بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ",
            SearchText = "بسم الله الرحمن الرحيم"
        };
        db.Surahs.Add(surah);
        db.Ayahs.Add(ayah);
        await db.SaveChangesAsync();

        var service = new AudioEditorService(db, new StubStorageService(), new StubAudioProcessor(), NullLogger<AudioEditorService>.Instance);
        var tokenizedCount = await service.TokenizeAyahsAsync(1);

        Assert.Equal(1, tokenizedCount);

        var words = await db.AyahWords.Where(w => w.AyahId == 1).OrderBy(w => w.WordIndex).ToListAsync();
        Assert.Equal(4, words.Count);
        Assert.Equal(1, words[0].WordIndex);
        Assert.Equal("بِسۡمِ", words[0].TextWarsh);
        Assert.Equal("بسم", words[0].SearchText);
        Assert.Equal("ٱلرَّحِيمِ", words[3].TextWarsh);
        Assert.Equal("الرحيم", words[3].SearchText);
    }
}
