using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Xunit;

namespace Quran.UnitTests;

public class CorpusResolutionServiceTests
{
    private static QuranDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<QuranDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new QuranDbContext(options);
    }

    [Fact]
    public async Task EnsureCorporaInitializedAsync_CreatesCorporaAndSeedsBaqarahMappings()
    {
        using var db = CreateInMemoryDbContext("CorpusResolution_Init");
        var service = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);

        await service.EnsureCorporaInitializedAsync();

        var corpora = await service.GetAllCorporaAsync();
        Assert.Equal(2, corpora.Count);
        Assert.Contains(corpora, c => c.Code == CorpusResolutionService.HistoricalCorpusCode);
        Assert.Contains(corpora, c => c.Code == CorpusResolutionService.MarwCorpusCode);

        var mappings = await service.GetMappingsForSurahAsync(2);
        Assert.Equal(5, mappings.Count);
    }

    [Fact]
    public async Task ResolveMapping_Baqarah1_ReturnsManyToOneToMarw1()
    {
        using var db = CreateInMemoryDbContext("CorpusResolution_Baqarah1");
        var service = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        await service.EnsureCorporaInitializedAsync();

        // 2:1 historical (الم) -> 2:1 MARW
        var result1 = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 1);

        Assert.True(result1.IsMapped);
        Assert.Equal("2:1", result1.TargetReference);
        Assert.Equal("ManyToOne", result1.Cardinality);
        Assert.Contains("الٓمٓ ليست آية مستقلة", result1.DifferenceDescription);

        // 2:2 historical (ذلك الكتاب...) -> 2:1 MARW
        var result2 = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 2);

        Assert.True(result2.IsMapped);
        Assert.Equal("2:1", result2.TargetReference);
        Assert.Equal("ManyToOne", result2.Cardinality);
    }

    [Fact]
    public async Task ResolveMapping_Baqarah4And5_DemonstratesOffsetDueToMerge()
    {
        using var db = CreateInMemoryDbContext("CorpusResolution_Baqarah4And5");
        var service = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        await service.EnsureCorporaInitializedAsync();

        // Historical 2:4 (والذين يؤمنون بما أنزل إليك...) -> MARW 2:3
        var result4 = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 4);

        Assert.True(result4.IsMapped);
        Assert.Equal("2:3", result4.TargetReference);
        Assert.Equal("OneToOne", result4.Cardinality);

        // Historical 2:5 (أولئك على هدى...) -> MARW 2:4 (concluding with المفلحون)
        var result5 = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 5);

        Assert.True(result5.IsMapped);
        Assert.Equal("2:4", result5.TargetReference);
        Assert.Contains("ٱلْمُفْلِحُونَ", result5.ProofNotes);
    }

    [Fact]
    public async Task ResolveMapping_UnmappedAyah_NeverFalselyAssumesOneToOne()
    {
        using var db = CreateInMemoryDbContext("CorpusResolution_Unmapped");
        var service = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        await service.EnsureCorporaInitializedAsync();

        // Ayah 2:6 is not yet seeded with explicit mapping
        var result6 = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 6);

        Assert.False(result6.IsMapped);
        Assert.Null(result6.TargetReference);
        Assert.Equal("Unmapped", result6.Cardinality);
        Assert.Equal("PendingReview", result6.ReviewStatus);
        Assert.Contains("لم يتم توثيق المطابقة", result6.DifferenceDescription);
    }

    [Fact]
    public async Task SaveMappingAsync_CanUpdateExistingMappingNotesAndStatus()
    {
        using var db = CreateInMemoryDbContext("CorpusResolution_Update");
        var service = new CorpusResolutionService(db, NullLogger<CorpusResolutionService>.Instance, null);
        await service.EnsureCorporaInitializedAsync();

        var mappings = await service.GetMappingsForSurahAsync(2);
        var first = mappings.First();

        first.ProofNotes = "Updated proof note from scholar review.";
        first.ReviewStatus = "Approved";

        await service.SaveMappingAsync(first);

        var updatedResult = await service.ResolveMappingAsync(
            CorpusResolutionService.HistoricalCorpusCode,
            CorpusResolutionService.MarwCorpusCode,
            2, 1);

        Assert.Equal("Updated proof note from scholar review.", updatedResult.ProofNotes);
    }
}
