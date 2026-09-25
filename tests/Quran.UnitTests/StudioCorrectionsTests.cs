using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class StudioCorrectionsTests
{
    private static async Task<(QuranDbContext Db, AnnotationService Service, Guid Source)> Setup()
    {
        var db = new QuranDbContext(new DbContextOptionsBuilder<QuranDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var corpus = new QuranCorpus { Code = CorpusResolutionService.MarwCorpusCode, NameArabic = "اختبار", CountingSystem = "test", Provenance = "fixture" };
        db.QuranCorpora.Add(corpus);
        db.Surahs.Add(new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Test", AyahCount = 286 });
        foreach (var n in new[] { 284, 285 }) db.CorpusAyahs.Add(new CorpusAyah { Corpus = corpus, SurahNumber = 2, AyahNumber = n, TextArabic = "fixture", SearchText = "fixture" });
        var source = new SourceAudio { Id = Guid.NewGuid(), SurahId = 2, StorageKey = "test.mp3", OriginalFileName = "test.mp3", DurationMs = 100000 };
        db.SourceAudios.Add(source);
        await db.SaveChangesAsync();
        return (db, new AnnotationService(db, NullLogger<AnnotationService>.Instance, new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance)), source.Id);
    }

    [Fact]
    public async Task ManualStartAndRetry_PreserveGapAndDoNotDuplicateOrRewindSession()
    {
        var (db, service, source) = await Setup();
        using (db)
        {
            var request = new CaptureEndAyahRequest(source, 18000, 14000, "2:284", null, null, null, MarwPilotCorpus.EditionCode, null, "capture-1");
            var first = await service.CaptureEndAyahAsync(request, "reviewer");
            Assert.Equal(14000, first.SavedOccurrence.StartMs);
            Assert.Equal(18000, first.NextStartMs);
            Assert.Equal(285, first.NextAyahNumber);
            await service.CaptureEndAyahAsync(request with { CurrentReference = "2:285", CurrentStartMs = 20000, CapturedTimeMs = 25000, IdempotencyKey = "capture-2" }, "reviewer");
            var retry = await service.CaptureEndAyahAsync(request, "reviewer");
            Assert.Equal(first.SavedOccurrence.Id, retry.SavedOccurrence.Id);
            Assert.Equal(2, await db.AudioSegments.CountAsync());
            Assert.Equal(25000, (await db.AnnotationSessions.SingleAsync()).CurrentTimeMs);
        }
    }

    [Fact]
    public async Task LastCanonicalAyah_DoesNotUseHistoricalSurahCount()
    {
        var (db, service, source) = await Setup();
        using (db)
        {
            var result = await service.CaptureEndAyahAsync(new(source, 18000, 14000, "2:285", null, null, null, MarwPilotCorpus.EditionCode, null, "last"), "reviewer");
            Assert.False(result.HasMoreAyahs);
            Assert.Null(result.NextReference);
            Assert.Null(result.SavedOccurrence.AyahId);
        }
    }

    [Fact]
    public async Task PartialThenContinuation_KeepTypesAndAdvanceOnlyAtCompletion()
    {
        var (db, service, source) = await Setup();
        using (db)
        {
            var request = new CaptureEndAyahRequest(source, 18000, 14000, "2:284", null, null, null, MarwPilotCorpus.EditionCode, null, "part", AudioSegmentType.PartialAyah);
            var part = await service.CaptureEndAyahAsync(request, "reviewer");
            Assert.Equal("2:284", part.NextReference);
            var tail = await service.CaptureEndAyahAsync(request with { CurrentStartMs = 18000, CapturedTimeMs = 22000, IdempotencyKey = "tail", SegmentType = AudioSegmentType.Continuation }, "reviewer");
            Assert.Equal("2:285", tail.NextReference);
            Assert.Equal(AudioSegmentType.Continuation, tail.SavedOccurrence.SegmentType);
        }
    }
}
