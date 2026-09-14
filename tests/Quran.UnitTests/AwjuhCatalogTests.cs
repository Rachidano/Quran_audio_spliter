using Microsoft.EntityFrameworkCore;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Xunit;

namespace Quran.UnitTests;

public class AwjuhCatalogTests
{
    [Fact]
    public void YasinGroupPreservesTenSourceBranchesAndSpecialConditions()
    {
        var entries = AwjuhCatalog.Read().GetProperty("entries").EnumerateArray()
            .Where(e => e.GetProperty("ruleGroup").GetString() == "yasin-opening").ToArray();
        Assert.Equal(Enumerable.Range(1, 10), entries.Select(e => e.GetProperty("sourceBranch").GetInt32()));
        Assert.All(entries, e => {
            Assert.Equal(36, e.GetProperty("application").GetProperty("sourceSurah").GetInt32());
            Assert.False(e.GetProperty("application").GetProperty("automaticAssignment").GetBoolean());
            Assert.NotEmpty(e.GetProperty("sourceBooks").EnumerateArray());
        });
        Assert.Contains("الغنة", entries[5].GetProperty("excludedScope").GetString());
        Assert.Equal("إظهار", entries[5].GetProperty("components").GetProperty("nunBeforeWaw").GetString());
        Assert.Contains("التعظيم", entries[6].GetProperty("excludedScope").GetString());
    }

    [Fact]
    public void CatalogEntriesHaveUniqueCodesRoutesAndSources()
    {
        var root = AwjuhCatalog.Read();
        var entries = root.GetProperty("entries").EnumerateArray()
            .Concat(root.GetProperty("constraints").EnumerateArray()).ToArray();
        Assert.Equal(entries.Length, entries.Select(e => e.GetProperty("code").GetString()).Distinct().Count());
        var routes = root.GetProperty("routes").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ToArray();
        var sources = root.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToArray();
        foreach (var entry in entries)
        {
            Assert.DoesNotContain(entry.GetProperty("code").GetString(), AwjuhCatalog.LegacyCodes);
            Assert.Contains(entry.GetProperty("routeCode").GetString(), routes);
            Assert.Equal("PendingReview", entry.GetProperty("status").GetString());
            Assert.NotEmpty(entry.GetProperty("sourceIds").EnumerateArray());
            foreach (var id in entry.GetProperty("sourceIds").EnumerateArray()) Assert.Contains(id.GetString(), sources);
        }
    }

    [Fact]
    public async Task RetirementPreservesAudioAndTimesAndRemovesApproval()
    {
        await using var db = new QuranDbContext(new DbContextOptionsBuilder<QuranDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Awjuh.Add(new Wajh { Id = 1, Code = "w01", NameArabic = "old" });
        var rec = new Recitation { Id = 1, WajhId = 1, AudioAssetId = 17, IsPublished = true, Status = RecitationStatus.Published };
        var segment = new AudioSegment { WajhId = 1, StartMs = 1234, EndMs = 5678, Status = AudioSegmentStatus.Approved, ValidatedAt = DateTimeOffset.UtcNow };
        db.Recitations.Add(rec); db.AudioSegments.Add(segment); await db.SaveChangesAsync();
        Assert.Equal(1, await AwjuhCatalog.ReplaceLegacyAsync(db));
        Assert.Empty(db.Awjuh); Assert.Null(rec.WajhId); Assert.False(rec.IsPublished); Assert.Equal(17, rec.AudioAssetId);
        Assert.Null(segment.WajhId); Assert.Equal(1234, segment.StartMs); Assert.Equal(5678, segment.EndMs);
        Assert.Equal(AudioSegmentStatus.Draft, segment.Status); Assert.Null(segment.ValidatedAt);
        Assert.Equal(0, await AwjuhCatalog.ReplaceLegacyAsync(db));
    }
}
