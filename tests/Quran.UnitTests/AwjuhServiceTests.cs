using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.UnitTests;

public class AwjuhServiceTests
{
    private static QuranDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuranDbContext(options);
    }

    [Fact]
    public async Task CreateDraft_ValidInput_CreatesDraftWithInitialRevision()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        var dto = new CreateWajhDto(
            Code: "test-badal-4-lin-4",
            NameArabic: "توسط البدل مع توسط اللين",
            RouteCode: "azraq-shatibiyya",
            Category: "Tahrirat",
            RuleGroup: "badal-lin",
            Description: "اجتماع مد البدل مع اللين المهموز بحسب الشاطبية",
            ExcludedScope: "لا يعمم على الطيبة",
            SourceCitation: "حرز الأماني، باب المد والقصر",
            Components: new List<WajhComponentDto>
            {
                new("badal", "توسط", 4),
                new("linMahmuz", "توسط", 4)
            }
        );

        var result = await service.CreateDraftAsync(dto, "admin-user");

        Assert.NotNull(result);
        Assert.Equal("test-badal-4-lin-4", result.Code);
        Assert.Equal("Draft", result.ReviewStatus);
        Assert.Equal(1, result.Version);
        Assert.Equal(2, result.Components.Count);
        Assert.Single(result.Revisions);
        Assert.Equal(1, result.Revisions[0].RevisionNumber);

        // Verify persistence
        var entity = await db.Awjuh.Include(w => w.Components).Include(w => w.Revisions).FirstOrDefaultAsync(w => w.Code == "test-badal-4-lin-4");
        Assert.NotNull(entity);
        Assert.Equal("Draft", entity.ReviewStatus);
        Assert.Equal(2, entity.Components.Count);
    }

    [Fact]
    public async Task CreateDraft_InvalidCode_ThrowsArgumentException()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        var dto = new CreateWajhDto(
            Code: "INVALID CODE WITH SPACES",
            NameArabic: "اسم تجريبي",
            RouteCode: "azraq-shatibiyya",
            Category: "Tahrirat",
            RuleGroup: null,
            Description: "وصف",
            ExcludedScope: null,
            SourceCitation: "مصدر"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateDraftAsync(dto, "admin"));
    }

    [Fact]
    public async Task UpdateApproved_CreatesNewRevision_AndInvalidatesDependentOccurrences()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        // Seed an Approved Wajh
        var wajh = new Wajh
        {
            Code = "az-sh-badal2-lin4",
            NameArabic = "قصر البدل مع توسط اللين",
            RouteCode = "azraq-shatibiyya",
            Description = "الشاطبية",
            SourceCitation = "حرز الأماني",
            ReviewStatus = "Approved",
            Version = 1
        };
        db.Awjuh.Add(wajh);

        // Add dependent AudioSegment and MushafPassage that were previously verified
        var segment = new AudioSegment
        {
            Wajh = wajh,
            Status = AudioSegmentStatus.Approved,
            ValidatedAt = DateTimeOffset.UtcNow,
            ValidatedByUserId = "reviewer-1",
            PublishedAudioAssetId = 99
        };
        var passage = new MushafPassage
        {
            Wajh = wajh,
            Status = "Approved"
        };
        db.AudioSegments.Add(segment);
        db.MushafPassages.Add(passage);
        await db.SaveChangesAsync();

        var updateDto = new UpdateWajhDto(
            NameArabic: "قصر البدل مع توسط اللين المهموز (معدل)",
            RouteCode: "azraq-shatibiyya",
            Category: "Tahrirat",
            RuleGroup: "badal-lin",
            Description: "تحديث الشروط والقيود",
            ExcludedScope: "قيد جديد",
            SourceCitation: "حرز الأماني وشروحه",
            Version: 1,
            ChangeReason: "إضافة قيود دقيقة على الاستثناءات"
        );

        var updated = await service.UpdateAsync(wajh.Id, updateDto, "editor-1");

        Assert.Equal(2, updated.Version);
        Assert.True(updated.Revisions.Count >= 1);

        // Verify dependent occurrences were reset to Draft for re-review
        var reloadedSegment = await db.AudioSegments.FirstAsync(s => s.Id == segment.Id);
        Assert.Equal(AudioSegmentStatus.Draft, reloadedSegment.Status);
        Assert.Null(reloadedSegment.ValidatedAt);
        Assert.Null(reloadedSegment.PublishedAudioAssetId);

        var reloadedPassage = await db.MushafPassages.FirstAsync(p => p.Id == passage.Id);
        Assert.Equal("Draft", reloadedPassage.Status);
    }

    [Fact]
    public async Task Review_ApproveAndReject_FollowsStateMachine()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        var wajh = new Wajh
        {
            Code = "review-test-01",
            NameArabic = "وجه للمراجعة",
            RouteCode = "asbahani-tayyiba",
            Description = "وصف",
            SourceCitation = "مصدر",
            ReviewStatus = "Draft",
            Version = 1
        };
        db.Awjuh.Add(wajh);
        await db.SaveChangesAsync();

        // 1. Submit
        var submitted = await service.ReviewAsync(wajh.Id, new ReviewWajhDto("Submit"), "author");
        Assert.Equal("PendingReview", submitted.ReviewStatus);

        // 2. Reject without reason -> throws ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(wajh.Id, new ReviewWajhDto("Reject", Reason: ""), "reviewer"));

        // 3. Reject with reason
        var rejected = await service.ReviewAsync(wajh.Id, new ReviewWajhDto("Reject", Reason: "المصدر غير دقيق"), "reviewer");
        Assert.Equal("Rejected", rejected.ReviewStatus);
        Assert.Equal("المصدر غير دقيق", rejected.RejectionReason);

        // 4. Re-submit and Approve
        await service.ReviewAsync(wajh.Id, new ReviewWajhDto("Submit"), "author");
        var approved = await service.ReviewAsync(wajh.Id, new ReviewWajhDto("Approve"), "senior-reviewer");
        Assert.Equal("Approved", approved.ReviewStatus);
        Assert.Equal("senior-reviewer", approved.ReviewerUserId);
        Assert.NotNull(approved.ReviewedAt);
    }

    [Fact]
    public async Task DeleteDraft_ForbiddenIfReferencedOrNotDraft()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        // Case A: Approved Wajh cannot be deleted
        var approvedWajh = new Wajh
        {
            Code = "approved-delete-test",
            NameArabic = "معتمد",
            ReviewStatus = "Approved",
            Version = 1
        };
        db.Awjuh.Add(approvedWajh);

        // Case B: Draft Wajh with an audio segment usage cannot be deleted
        var draftWithUsage = new Wajh
        {
            Code = "draft-with-usage",
            NameArabic = "مسودة مستخدمة",
            ReviewStatus = "Draft",
            Version = 1
        };
        db.Awjuh.Add(draftWithUsage);
        db.AudioSegments.Add(new AudioSegment
        {
            Wajh = draftWithUsage,
            StartMs = 0,
            EndMs = 5000
        });

        // Case C: Unused draft can be deleted
        var cleanDraft = new Wajh
        {
            Code = "clean-draft",
            NameArabic = "مسودة قابلة للحذف",
            ReviewStatus = "Draft",
            Version = 1
        };
        db.Awjuh.Add(cleanDraft);
        await db.SaveChangesAsync();

        // Check A
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftAsync(approvedWajh.Id, 1));

        // Check B
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftAsync(draftWithUsage.Id, 1));

        // Check C - successful deletion
        await service.DeleteDraftAsync(cleanDraft.Id, 1);
        Assert.Null(await db.Awjuh.FirstOrDefaultAsync(w => w.Code == "clean-draft"));
    }

    [Fact]
    public async Task DuplicateDraft_CreatesNewDraftWithoutApproval()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        var approved = new Wajh
        {
            Code = "source-wajh",
            NameArabic = "وجه معتمد أصلي",
            RouteCode = "azraq-shatibiyya",
            Description = "وصف أصلي",
            SourceCitation = "مصدر أصلي",
            ReviewStatus = "Approved",
            Version = 3
        };
        approved.Components.Add(new WajhComponent { ComponentKey = "badal", ValueArabic = "4", ValueNumeric = 4 });
        db.Awjuh.Add(approved);
        await db.SaveChangesAsync();

        var duplicated = await service.DuplicateAsync(approved.Id, "duplicated-wajh", "user-1");

        Assert.NotNull(duplicated);
        Assert.Equal("duplicated-wajh", duplicated.Code);
        Assert.Equal("Draft", duplicated.ReviewStatus); // Never copy approval
        Assert.Equal(1, duplicated.Version);
        Assert.Single(duplicated.Components);
        Assert.Equal("4", duplicated.Components[0].ValueArabic);
    }

    [Fact]
    public async Task ReaderAvailabilityPolicy_OnlyReturnsApprovedWithPublishedAudio()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        // 1. Approved with published segment -> AVAILABLE
        var w1 = new Wajh { Id = 101, Code = "w1-approved-published", NameArabic = "متاح 1", ReviewStatus = "Approved" };
        var s1 = new AudioSegment { WajhId = 101, Reference = "1:1", Status = AudioSegmentStatus.Approved, PublishedAudioAssetId = 1001 };

        // 2. Approved WITHOUT published segment -> NOT AVAILABLE
        var w2 = new Wajh { Id = 102, Code = "w2-approved-no-audio", NameArabic = "غير متاح 2", ReviewStatus = "Approved" };

        // 3. Draft WITH published segment -> NOT AVAILABLE (Must be Approved)
        var w3 = new Wajh { Id = 103, Code = "w3-draft-with-audio", NameArabic = "غير متاح 3", ReviewStatus = "Draft" };
        var s3 = new AudioSegment { WajhId = 103, Reference = "1:1", Status = AudioSegmentStatus.Approved, PublishedAudioAssetId = 1003 };

        db.Awjuh.AddRange(w1, w2, w3);
        db.AudioSegments.AddRange(s1, s3);
        await db.SaveChangesAsync();

        var readerAwjuh = await service.GetAvailableAwjuhForReaderAsync(1, "1:1");

        Assert.Single(readerAwjuh);
        Assert.Equal("w1-approved-published", readerAwjuh[0].Code);
    }

    [Fact]
    public async Task SafeSeed_NeverResurrectsDeletedLegacyCodes()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);

        // Run seed
        await service.EnsureSeedDataAsync(force: false);

        var allAwjuh = await db.Awjuh.AsNoTracking().ToListAsync();

        // Check none of the deleted legacy codes exist
        foreach (var legacy in AwjuhCatalog.LegacyCodes)
        {
            Assert.DoesNotContain(allAwjuh, w => w.Code == legacy);
        }

        // Verify the 14 valid documented proposals are seeded
        Assert.Equal(14, allAwjuh.Count);
        Assert.Contains(allAwjuh, w => w.Code == "az-sh-badal2-lin4");
        Assert.Contains(allAwjuh, w => w.Code == "as-ty-ys-01");
        Assert.Contains(allAwjuh, w => w.Code == "as-ty-ys-10");

        // Verify structured relations were populated
        var routes = await db.TransmissionRoutes.ToListAsync();
        Assert.Equal(3, routes.Count);

        var sources = await db.ReferenceSources.ToListAsync();
        Assert.Equal(5, sources.Count);

        var constraints = await db.CombinationConstraints.ToListAsync();
        Assert.Equal(4, constraints.Count);

        var coverage = await db.DocumentaryCoverageEntries.ToListAsync();
        Assert.Equal(4, coverage.Count);

        // Delete one draft
        var toDelete = await db.Awjuh.FirstAsync(w => w.Code == "as-ty-ys-10");
        toDelete.ReviewStatus = "Draft";
        await db.SaveChangesAsync();
        await service.DeleteDraftAsync(toDelete.Id, toDelete.Version);
        Assert.Null(await db.Awjuh.FirstOrDefaultAsync(w => w.Code == "as-ty-ys-10"));

        // Call EnsureSeedDataAsync again (simulating app restart)
        await service.EnsureSeedDataAsync(force: false);

        // Verify that deleted wajh was NOT resurrected!
        Assert.Null(await db.Awjuh.FirstOrDefaultAsync(w => w.Code == "as-ty-ys-10"));
    }

    [Fact]
    public async Task DocumentaryCoverageMatrix_CanBeQueriedAndUpdated()
    {
        await using var db = CreateInMemoryDb();
        var service = new AwjuhService(db);
        await service.EnsureSeedDataAsync(force: false);

        var matrix = await service.GetCoverageMatrixAsync("asbahani-tayyiba");
        Assert.NotEmpty(matrix);

        var yasinEntry = matrix.First(e => e.ChapterOrSection == "سورة يس");
        Assert.Equal("AResoudre", yasinEntry.AnalysisStatus);

        // Update status to Revue
        var updated = await service.UpdateCoverageEntryAsync(new UpdateCoverageEntryDto(
            Id: yasinEntry.Id,
            AnalysisStatus: "Revue",
            Notes: "تم التدقيق والمطابقة الكاملة مع مصحف MARW"
        ), "reviewer-marw");

        Assert.Equal("Revue", updated.AnalysisStatus);
        Assert.Equal("تم التدقيق والمطابقة الكاملة مع مصحف MARW", updated.Notes);
    }
}
