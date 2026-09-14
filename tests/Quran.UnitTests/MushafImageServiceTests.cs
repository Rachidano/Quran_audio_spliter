using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Application.DTOs;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class MushafImageServiceTests
{
    [Fact]
    public async Task MissingLegacyPage_DoesNotCreateEditionOrDownload()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        Assert.Null(await service.GetPageAsync(2));
        Assert.Empty(db.MushafEditions);
        Assert.Empty(db.MushafPages);
    }

    [Fact]
    public async Task LegacyArchiveImport_IsDisabled()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        await Assert.ThrowsAsync<NotSupportedException>(() => service.ImportPageAsync(2));
        Assert.Empty(db.MushafEditions);
        Assert.Empty(db.MushafPages);
    }

    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new QuranDbContext(options);
    }

    [Fact]
    public async Task GetOrCreateDefaultEdition_CreatesMadinahWarshEdition()
    {
        using var db = CreateInMemoryDbContext("Test_Mushaf_DefaultEdition");
        var riwaya = new Riwaya { Id = 1, NameArabic = "ورش عن نافع", NameLatin = "Warsh" };
        db.Riwayas.Add(riwaya);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var service = new MushafImageService(db, config, NullLogger<MushafImageService>.Instance);

        var edition = await service.GetOrCreateDefaultEditionAsync();

        Assert.NotNull(edition);
        Assert.Equal("MADINAH_WARSH", edition.Code);
        Assert.Equal("مصحف المدينة النبوية - رواية ورش عن نافع", edition.NameArabic);
    }

    [Fact]
    public async Task CreateAyahRegion_ValidatesNormalizedBounds()
    {
        using var db = CreateInMemoryDbContext("Test_Mushaf_BoundsValidation");
        var surah = new Surah { Id = 1, Number = 1, NameArabic = "الفاتحة", NameLatin = "Al-Fatihah", AyahCount = 7 };
        var ayah = new Ayah { Id = 1, SurahId = 1, AyahNumber = 1, NumberInQuran = 1, TextWarsh = "بِسۡمِ ٱللَّهِ" };
        var page = new MushafPage { Id = 1, MushafEditionId = 1, PageNumber = 1, MasterStorageKey = "mushaf/masters/warsh/page_001.jpg", WebStorageKey = "mushaf/web/warsh/page_001.webp", Width = 750, Height = 1072 };

        db.Surahs.Add(surah);
        db.Ayahs.Add(ayah);
        db.MushafPages.Add(page);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var service = new MushafImageService(db, config, NullLogger<MushafImageService>.Instance);

        // X < 0
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah.Id, page.Id, -0.1m, 0.2m, 0.5m, 0.1m)));

        // Width <= 0
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah.Id, page.Id, 0.1m, 0.2m, 0.0m, 0.1m)));

        // X + Width > 1
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(ayah.Id, page.Id, 0.8m, 0.2m, 0.4m, 0.1m)));

        // Valid region
        var validReq = new CreateAyahRegionRequest(ayah.Id, page.Id, 0.15m, 0.25m, 0.70m, 0.08m, 1, 1);
        var created = await service.CreateAyahRegionAsync(validReq);

        Assert.NotNull(created);
        Assert.Equal(0.15m, created.X);
        Assert.Equal(0.25m, created.Y);
        Assert.Equal(0.70m, created.Width);
        Assert.Equal(0.08m, created.Height);
        Assert.Equal(1, created.Sequence);
    }

    [Fact]
    public async Task AyahImageRegion_XMustBeNormalized()
    {
        using var db = CreateInMemoryDbContext("Test_AyahImageRegion_XMustBeNormalized");
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, -0.05m, 0.2m, 0.5m, 0.1m)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 1.05m, 0.2m, 0.5m, 0.1m)));
    }

    [Fact]
    public async Task AyahImageRegion_YMustBeNormalized()
    {
        using var db = CreateInMemoryDbContext("Test_AyahImageRegion_YMustBeNormalized");
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, -0.05m, 0.5m, 0.1m)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 1.05m, 0.5m, 0.1m)));
    }

    [Fact]
    public async Task AyahImageRegion_WidthMustBeValid()
    {
        using var db = CreateInMemoryDbContext("Test_AyahImageRegion_WidthMustBeValid");
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 0.2m, 0m, 0.1m)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 0.2m, -0.2m, 0.1m)));
    }

    [Fact]
    public async Task AyahImageRegion_HeightMustBeValid()
    {
        using var db = CreateInMemoryDbContext("Test_AyahImageRegion_HeightMustBeValid");
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 0.2m, 0.5m, 0m)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 0.2m, 0.5m, -0.1m)));
    }

    [Fact]
    public async Task AyahImageRegion_MustFitInsidePage()
    {
        using var db = CreateInMemoryDbContext("Test_AyahImageRegion_MustFitInsidePage");
        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);
        // X + Width > 1.01
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.7m, 0.2m, 0.4m, 0.1m)));
        // Y + Height > 1.01
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAyahRegionAsync(new CreateAyahRegionRequest(1, 1, 0.1m, 0.8m, 0.5m, 0.3m)));
    }

    [Fact]
    public async Task AudioSegmentImageRegion_ValidationAndCreation_Succeeds()
    {
        using var db = CreateInMemoryDbContext("Test_AudioSegmentImageRegion");
        var segId = Guid.NewGuid();
        var segment = new AudioSegment
        {
            Id = segId,
            SourceAudioId = Guid.NewGuid(),
            AyahId = 1,
            StartMs = 0,
            EndMs = 5000,
            Status = Quran.Domain.Enums.AudioSegmentStatus.Draft
        };
        var page = new MushafPage { Id = 1, MushafEditionId = 1, PageNumber = 1, MasterStorageKey = "mushaf/masters/warsh/page_001.jpg", WebStorageKey = "mushaf/web/warsh/page_001.webp", Width = 750, Height = 1072 };
        db.AudioSegments.Add(segment);
        db.MushafPages.Add(page);
        await db.SaveChangesAsync();

        var service = new MushafImageService(db, new ConfigurationBuilder().Build(), NullLogger<MushafImageService>.Instance);

        var req = new CreateSegmentImageRegionRequest(segId, page.Id, 0.1m, 0.2m, 0.8m, 0.05m, 1);
        var created = await service.CreateSegmentImageRegionAsync(req);

        Assert.NotNull(created);
        Assert.Equal(segId, created.AudioSegmentId);
        Assert.Equal(0.1m, created.X);
        Assert.Equal(0.8m, created.Width);

        var list = await service.GetSegmentImageRegionsAsync(segId);
        Assert.Single(list);
    }
}
