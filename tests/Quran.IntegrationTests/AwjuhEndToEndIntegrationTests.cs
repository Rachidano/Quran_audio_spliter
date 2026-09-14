using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.IntegrationTests;

public class AwjuhEndToEndIntegrationTests
{
    private static QuranDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuranDbContext(options);
    }

    [Fact]
    public async Task CompleteLot4SortieFlow_Create_EditorFinds_Assign_ApproveBoth_ReaderShows_ModifyInvalidates_ArchiveProtects()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        // 1. Create a definition in test DB
        var createDto = new CreateWajhDto(
            Code: "test-sortie-wajh",
            NameArabic: "وجه اختباري لمسار الاعتماد الكامل",
            RouteCode: "azraq-shatibiyya",
            Category: "Tahrirat",
            RuleGroup: "badal-lin",
            Description: "اختبار معايير خروج اللوت 4",
            ExcludedScope: null,
            SourceCitation: "توثيق تجريبي",
            Components: new List<WajhComponentDto>
            {
                new("badal", "توسط", 4)
            },
            Scopes: new List<WajhScopeDto>
            {
                new(SourceSurah: 2, SourceAyahStart: 1, SourceAyahEnd: 5)
            }
        );

        var createdWajh = await service.CreateDraftAsync(createDto, "admin-tester");
        Assert.Equal("Draft", createdWajh.ReviewStatus);

        // Submit to review so editor sees it
        await service.ReviewAsync(createdWajh.Id, new ReviewWajhDto("Submit"), "admin-tester");

        // 2. Editor candidates find it
        var editorCandidates = await service.GetCandidateAwjuhForEditorAsync(surahNumber: 2, reference: "2:1");
        Assert.Contains(editorCandidates, w => w.Code == "test-sortie-wajh");

        // Reader does NOT propose it yet (neither wajh nor audio is approved)
        var readerBeforeApproval = await service.GetAvailableAwjuhForReaderAsync(surahNumber: 2, reference: "2:1");
        Assert.DoesNotContain(readerBeforeApproval, w => w.Code == "test-sortie-wajh");

        // 3. Assign to an audio segment
        var sourceAudio = new SourceAudio
        {
            Id = Guid.NewGuid(),
            SurahId = 2,
            StorageKey = "sources/002.mp3",
            OriginalFileName = "002.mp3",
            ChecksumSha256 = "hash123"
        };
        db.SourceAudios.Add(sourceAudio);

        var segment = new AudioSegment
        {
            Id = Guid.NewGuid(),
            SourceAudioId = sourceAudio.Id,
            Reference = "2:1",
            StartMs = 13974,
            EndMs = 22883,
            WajhId = createdWajh.Id,
            Status = AudioSegmentStatus.Draft
        };
        db.AudioSegments.Add(segment);
        await db.SaveChangesAsync();

        // Check usage report shows 1 usage
        var reportBefore = await service.GetUsageReportAsync(createdWajh.Id);
        Assert.Equal(1, reportBefore.TotalUsages);
        Assert.Equal(1, reportBefore.AudioSegmentsCount);

        // 4. Approve both Wajh and Segment
        await service.ReviewAsync(createdWajh.Id, new ReviewWajhDto("Approve"), "senior-reviewer");

        segment.Status = AudioSegmentStatus.Approved;
        segment.PublishedAudioAssetId = 777; // simulate published asset
        segment.ValidatedAt = DateTimeOffset.UtcNow;
        segment.ValidatedByUserId = "senior-reviewer";
        await db.SaveChangesAsync();

        // 5. Reader now proposes it!
        var readerAfterApproval = await service.GetAvailableAwjuhForReaderAsync(surahNumber: 2, reference: "2:1");
        Assert.Contains(readerAfterApproval, w => w.Code == "test-sortie-wajh");

        var currentWajh = await service.GetByIdAsync(createdWajh.Id);
        Assert.NotNull(currentWajh);

        // 6. Modify the approved definition according to policy -> creates revision and invalidates dependent occurrence
        var updateDto = new UpdateWajhDto(
            NameArabic: "وجه اختباري معدل وفق السياسة",
            RouteCode: "azraq-shatibiyya",
            Category: "Tahrirat",
            RuleGroup: "badal-lin",
            Description: "تحديث الشروط",
            ExcludedScope: "قيد إضافي",
            SourceCitation: "توثيق محدث",
            Version: currentWajh.Version,
            ChangeReason: "تعديل سياق التحرير"
        );

        var updatedWajh = await service.UpdateAsync(createdWajh.Id, updateDto, "lead-editor");
        Assert.True(updatedWajh.Version >= 3);
        Assert.True(updatedWajh.Revisions.Count >= 2);

        // Verify segment was invalidated back to Draft
        var reloadedSegment = await db.AudioSegments.FirstAsync(s => s.Id == segment.Id);
        Assert.Equal(AudioSegmentStatus.Draft, reloadedSegment.Status);
        Assert.Null(reloadedSegment.PublishedAudioAssetId);

        // Reader no longer proposes it (since segment returned to Draft)
        var readerAfterInvalidation = await service.GetAvailableAwjuhForReaderAsync(surahNumber: 2, reference: "2:1");
        Assert.DoesNotContain(readerAfterInvalidation, w => w.Code == "test-sortie-wajh");

        // 7. Archive the Wajh
        await service.ReviewAsync(createdWajh.Id, new ReviewWajhDto("Archive", Reason: "سحب الوجه لانتفاء التوثيق"), "admin-archiver");
        var reloadedWajh = await service.GetByIdAsync(createdWajh.Id);
        Assert.Equal("Archived", reloadedWajh!.ReviewStatus);

        // Cannot be deleted because it has history and usages
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftAsync(createdWajh.Id, reloadedWajh.Version));

        // Impact report still accurately details all historical links
        var finalReport = await service.GetUsageReportAsync(createdWajh.Id);
        Assert.Equal(1, finalReport.TotalUsages);
        Assert.Equal(1, finalReport.AudioSegmentsCount);
        Assert.Equal("Archived", finalReport.ReviewStatus);
    }
}
