using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class AdminValidationServiceTests
{
    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new QuranDbContext(options);
    }

    [Fact]
    public async Task ValidationWorkflow_Approve_Publish_Reject_WorksAsExpected()
    {
        using var dbContext = CreateInMemoryDbContext("Quran_Validation_Workflow");

        var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 7 };
        var ayah = new Ayah { Id = 1, SurahId = 1, AyahNumber = 1, NumberInQuran = 1, TextWarsh = "بِسۡمِ ٱللَّهِ", SearchText = "بسم الله" };
        var reciter = new Reciter { Id = 1, NameArabic = "صابر عبد الحكم", NameLatin = "Saber Abdelhakam" };
        var riwaya = new Riwaya { Id = 1, NameArabic = "ورش عن نافع", NameLatin = "Warsh" };
        var wajh1 = new Wajh { Id = 1, Code = "w01", NameArabic = "الوجه 1", ReviewStatus = "Approved" };
        var wajh2 = new Wajh { Id = 2, Code = "w02", NameArabic = "الوجه 2", ReviewStatus = "Approved" };
        var asset = new AudioAsset { Id = 1, StorageKey = "published/001/001/warsh/w01.mp3", Checksum = "hash1" };

        var recitation = new Recitation
        {
            Id = 1,
            AyahId = 1,
            ReciterId = 1,
            RiwayaId = 1,
            WajhId = 1,
            AudioAssetId = 1,
            Status = RecitationStatus.Imported,
            IsPublished = false
        };

        var segment = new AudioSegment
        {
            Id = Guid.NewGuid(),
            PublishedAudioAssetId = 1,
            Status = AudioSegmentStatus.Published,
            Edition = MarwPilotCorpus.EditionCode
        };

        dbContext.Surahs.Add(surah);
        dbContext.Ayahs.Add(ayah);
        dbContext.Reciters.Add(reciter);
        dbContext.Riwayas.Add(riwaya);
        dbContext.Awjuh.AddRange(wajh1, wajh2);
        dbContext.AudioAssets.Add(asset);
        dbContext.AudioSegments.Add(segment);
        dbContext.Recitations.Add(recitation);
        await dbContext.SaveChangesAsync();

        var service = new AdminValidationService(dbContext, NullLogger<AdminValidationService>.Instance);

        // 1. Approve
        await service.ApproveRecitationAsync(1, "Validé par Cheikh", "validator1");
        var approved = await dbContext.Recitations.Include(r => r.ValidationReviews).FirstAsync(r => r.Id == 1);
        Assert.Equal(RecitationStatus.Approved, approved.Status);
        Assert.False(approved.IsPublished); // RG-06: Not yet published
        Assert.Single(approved.ValidationReviews);

        // 2. Publish
        await service.PublishRecitationAsync(1, "Prêt pour diffusion", "admin1");
        var published = await dbContext.Recitations.FirstAsync(r => r.Id == 1);
        Assert.Equal(RecitationStatus.Published, published.Status);
        Assert.True(published.IsPublished);

        // 3. Update Wajh
        await service.UpdateWajhAsync(1, 2);
        var updated = await dbContext.Recitations.FirstAsync(r => r.Id == 1);
        Assert.Equal(2, updated.WajhId);

        // 4. Reject with empty comment throws
        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectRecitationAsync(1, "", "admin1"));

        // 5. Reject with reason
        await service.RejectRecitationAsync(1, "Bruit de fond excessif", "admin1");
        var rejected = await dbContext.Recitations.FirstAsync(r => r.Id == 1);
        Assert.Equal(RecitationStatus.Rejected, rejected.Status);
        Assert.False(rejected.IsPublished);
    }
}
