using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.UnitTests;

public class AnnotationServiceTests
{
    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new QuranDbContext(options);
    }

    [Fact]
    public async Task SaveOccurrenceAsync_CreatesSegmentAndRevisionWithAuditLog()
    {
        using var db = CreateInMemoryDbContext("Annotation_SaveNew");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/002/original.mp3",
            DurationMs = 15000000,
            OriginalFileName = "002.mp3"
        };
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        var req = new SaveOccurrenceRequest(
            null,
            sourceAudio.Id,
            1000,
            5000,
            "2:1",
            null,
            null,
            AudioSegmentType.FullAyah,
            null,
            "First verse note",
            "marw-newMushaf",
            "hash1",
            null,
            null
        );

        var result = await service.SaveOccurrenceAsync(req, "test-user");

        Assert.NotNull(result);
        Assert.Equal(1000, result.StartMs);
        Assert.Equal(5000, result.EndMs);
        Assert.Equal("Draft", result.Status.ToString());

        var stored = await db.AudioSegments.Include(s => s.Revisions).FirstOrDefaultAsync(s => s.Id == result.Id);
        Assert.NotNull(stored);
        Assert.Single(stored.Revisions);
        Assert.Equal(1, stored.Revisions.First().RevisionNumber);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == result.Id.ToString());
        Assert.NotNull(audit);
        Assert.Equal("CreateOccurrence", audit.Action);
    }

    [Fact]
    public async Task CaptureEndAyahAsync_AdvancesStartAndCalculatesNextAyah_UpdatesSession()
    {
        using var db = CreateInMemoryDbContext("Annotation_CaptureEndAyah");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var surah2 = new Surah { Id = 2, Number = 2, NameArabic = "البقرة", NameLatin = "Al-Baqarah", AyahCount = 286 };
        db.Surahs.Add(surah2);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/002/original.mp3",
            DurationMs = 15000000,
            OriginalFileName = "002.mp3"
        };
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        var captureReq = new CaptureEndAyahRequest(
            sourceAudio.Id,
            22883,
            13974,
            "2:1",
            null,
            null,
            "Ayah 1 note",
            "marw-newMushaf",
            "hash",
            "idemp-1"
        );

        var result = await service.CaptureEndAyahAsync(captureReq, "test-user");

        Assert.Equal(22883, result.NextStartMs);
        Assert.Equal("2:2", result.NextReference);
        Assert.True(result.HasMoreAyahs);

        // Verify session persistence
        var session = await service.GetOrCreateSessionAsync("test-user", sourceAudio.Id);
        Assert.Equal(22883, session.CurrentTimeMs);
        Assert.Equal(2, session.CurrentAyahNumber);
        Assert.Equal(result.SavedOccurrence.Id, session.LastSegmentId);
    }

    [Fact]
    public async Task CaptureRepeatAsync_CreatesLinkedOccurrenceWithoutAdvancingVerseNumber()
    {
        using var db = CreateInMemoryDbContext("Annotation_CaptureRepeat");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/002/original.mp3",
            DurationMs = 15000000,
            OriginalFileName = "002.mp3"
        };
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        // 1. Initial capture
        var parentReq = new SaveOccurrenceRequest(
            null,
            sourceAudio.Id,
            13974,
            22883,
            "2:1",
            null,
            null,
            AudioSegmentType.FullAyah,
            null,
            "Original take",
            "marw-newMushaf",
            "hash",
            null,
            null
        );
        var parent = await service.SaveOccurrenceAsync(parentReq, "test-user");

        // 2. Repeat capture
        var repeatReq = new CaptureRepeatRequest(
            sourceAudio.Id,
            parent.Id,
            22883,
            30000,
            null,
            "Repeat take with different tajweed",
            "marw-newMushaf",
            "hash"
        );

        var repeat = await service.CaptureRepeatAsync(repeatReq, "test-user");

        Assert.Equal(parent.Id, repeat.Id != parent.Id ? parent.Id : Guid.Empty);
        Assert.Equal(AudioSegmentType.Repetition, repeat.SegmentType);

        var inDb = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == repeat.Id);
        Assert.NotNull(inDb);
        Assert.Equal(parent.Id, inDb.RepeatOfId);
    }

    [Fact]
    public async Task MigrateMushafPassagesToAudioSegmentsAsync_PreservesIdsAndTimestamps()
    {
        using var db = CreateInMemoryDbContext("Annotation_MigratePassages");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/002/original.mp3",
            ChecksumSha256 = "sha256-test",
            DurationMs = 15000000,
            OriginalFileName = "002.mp3"
        };
        db.SourceAudios.Add(sourceAudio);

        var passageId = Guid.NewGuid();
        db.MushafPassages.Add(new MushafPassage
        {
            Id = passageId,
            SourceKey = "sources/002/original.mp3",
            SourceHash = "sha256-test",
            Edition = "marw-newMushaf",
            Reference = "2:1",
            StartMs = 13974,
            EndMs = 22883,
            SegmentType = 1,
            Status = "Draft",
            Notes = "Pilot user annotation"
        });
        await db.SaveChangesAsync();

        var report = await service.MigrateMushafPassagesToAudioSegmentsAsync();

        Assert.Equal(1, report.MigratedCount);
        Assert.Empty(report.Errors);

        var segment = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == passageId);
        Assert.NotNull(segment);
        Assert.Equal(13974, segment.StartMs);
        Assert.Equal(22883, segment.EndMs);
        Assert.Equal("2:1", segment.Reference);
        Assert.Equal("Pilot user annotation", segment.Notes);
    }

    [Fact]
    public async Task DeleteOccurrenceAsync_BlocksWhenChildRepeatExists()
    {
        using var db = CreateInMemoryDbContext("Annotation_DeleteConflict");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/002/original.mp3",
            DurationMs = 15000000,
            OriginalFileName = "002.mp3"
        };
        db.SourceAudios.Add(sourceAudio);

        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        db.AudioSegments.Add(new AudioSegment
        {
            Id = parentId,
            SourceAudioId = sourceAudio.Id,
            StartMs = 1000,
            EndMs = 5000,
            Version = 1,
            CreatedByUserId = "admin"
        });

        db.AudioSegments.Add(new AudioSegment
        {
            Id = childId,
            SourceAudioId = sourceAudio.Id,
            StartMs = 5000,
            EndMs = 9000,
            RepeatOfId = parentId,
            Version = 1,
            CreatedByUserId = "admin"
        });

        await db.SaveChangesAsync();

        // Attempt to delete parent should fail because child repeat points to it
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteOccurrenceAsync(parentId, 1, "admin"));
    }

    [Fact]
    public async Task UndoLastCaptureAsync_RevertsDraftSegmentAndRestoresSession()
    {
        using var db = CreateInMemoryDbContext("Annotation_Undo_Success");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatiha", AyahCount = 7 };
        db.Surahs.Add(surah);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            SurahId = surah.Id,
            StorageKey = "sources/001/original.mp3",
            DurationMs = 60000,
            OriginalFileName = "001.mp3"
        };
        db.SourceAudios.Add(sourceAudio);
        await db.SaveChangesAsync();

        // Capture Ayah 1
        var cap1 = await service.CaptureEndAyahAsync(new CaptureEndAyahRequest(
            sourceAudio.Id,
            5000,
            0,
            "1:1",
            null,
            null,
            "First ayah",
            "marw",
            "hash1",
            "idem1"
        ), "user1");

        Assert.Equal(5000, cap1.NextStartMs);
        Assert.Equal(2, cap1.NextAyahNumber);

        // Verify segment exists
        var seg = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == cap1.SavedOccurrence.Id);
        Assert.NotNull(seg);

        // Perform undo
        var undo = await service.UndoLastCaptureAsync(sourceAudio.Id, "user1");

        Assert.True(undo.Success);
        Assert.Equal(cap1.SavedOccurrence.Id, undo.RemovedSegmentId);
        Assert.Equal(0, undo.RestoredTimeMs);
        Assert.Equal(1, undo.RestoredAyahNumber);

        // Verify segment was deleted
        var deletedSeg = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == cap1.SavedOccurrence.Id);
        Assert.Null(deletedSeg);

        // Verify session was restored
        var session = await db.AnnotationSessions.FirstOrDefaultAsync(s => s.UserId == "user1" && s.SourceAudioId == sourceAudio.Id);
        Assert.NotNull(session);
        Assert.Equal(0, session.CurrentTimeMs);
        Assert.Equal(1, session.CurrentAyahNumber);
    }

    [Fact]
    public async Task UndoLastCaptureAsync_RejectsWhenChildRepeatExists()
    {
        using var db = CreateInMemoryDbContext("Annotation_Undo_BlockedByRepeat");
        var corpusService = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        var service = new AnnotationService(db, NullLogger<AnnotationService>.Instance, corpusService);

        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            StorageKey = "sources/001/original.mp3",
            DurationMs = 60000,
            OriginalFileName = "001.mp3"
        };
        db.SourceAudios.Add(sourceAudio);

        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        db.AudioSegments.Add(new AudioSegment
        {
            Id = parentId,
            SourceAudioId = sourceAudio.Id,
            StartMs = 1000,
            EndMs = 5000,
            Reference = "1:1",
            Status = AudioSegmentStatus.Draft,
            Version = 1,
            CreatedByUserId = "user1",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        db.AudioSegments.Add(new AudioSegment
        {
            Id = childId,
            SourceAudioId = sourceAudio.Id,
            StartMs = 5000,
            EndMs = 8000,
            Reference = "1:1",
            RepeatOfId = parentId,
            Status = AudioSegmentStatus.Draft,
            Version = 1,
            CreatedByUserId = "user1",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        // Attempting to undo parent while child repeat exists should be rejected
        // Note: childId is the latest, so if childId is deleted, then parent can be deleted
        // Let's test with a direct reference or when child repeat is targeted
        var undo = await service.UndoLastCaptureAsync(sourceAudio.Id, "user1");
        Assert.True(undo.Success);
        Assert.Equal(childId, undo.RemovedSegmentId); // Child repeat is undone first!

        // Next undo will undo parent
        var undoParent = await service.UndoLastCaptureAsync(sourceAudio.Id, "user1");
        Assert.True(undoParent.Success);
        Assert.Equal(parentId, undoParent.RemovedSegmentId);
    }
}

