using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;
using Quran.Web.Controllers;

namespace Quran.IntegrationTests;

public class AudioDownloadControllerTests : IDisposable
{
    private readonly QuranDbContext db = new(new DbContextOptionsBuilder<QuranDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly string root = Path.Combine(Path.GetTempPath(), "quran-download-" + Guid.NewGuid().ToString("N"));
    private readonly AudioDownloadsController controller;
    private readonly AudioSegment segment;
    public AudioDownloadControllerTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "published"));
        Directory.CreateDirectory(Path.Combine(root, "sources", "002"));
        File.WriteAllText(Path.Combine(root, "published", "test.mp3"), "fixture");
        File.WriteAllText(Path.Combine(root, "sources", "002", "original.mp3"), "fixture");
        var corpus = new QuranCorpus { Code = "marw-newMushaf", NameArabic = "test", CountingSystem = "test", Provenance = "fixture" };
        db.CorpusAyahs.Add(new CorpusAyah { Corpus = corpus, SurahNumber = 2, AyahNumber = 1, TextArabic = "", SearchText = "" });
        var surah = new Surah { Number = 2, NameArabic = "test", NameLatin = "test" };
        var source = new SourceAudio { Surah = surah, StorageKey = "sources/002/original.mp3" };
        segment = new AudioSegment { SourceAudio = source, Reference = "2:1", Edition = MarwPilotCorpus.EditionCode,
            SegmentType = AudioSegmentType.FullAyah, Status = AudioSegmentStatus.Published,
            PublishedAudioAsset = new AudioAsset { StorageKey = "published/test.mp3" } };
        db.AudioSegments.Add(segment); db.SaveChanges();
        controller = new AudioDownloadsController(db, new Storage(root)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }
    [Fact]
    public async Task PublishedFullAyah_DownloadHasAttachmentNameAndRanges()
    {
        var result = Assert.IsType<PhysicalFileResult>(await controller.Ayah(segment.Id, default));
        Assert.StartsWith("warsh-002-001-", result.FileDownloadName);
        Assert.True(result.EnableRangeProcessing);
    }
    [Theory]
    [InlineData(AudioSegmentStatus.Draft, AudioSegmentType.FullAyah)]
    [InlineData(AudioSegmentStatus.Published, AudioSegmentType.PartialAyah)]
    [InlineData(AudioSegmentStatus.Published, AudioSegmentType.Repetition)]
    public async Task DraftOrIncompleteAyah_CannotBeDownloadedAsComplete(AudioSegmentStatus status, AudioSegmentType type)
    {
        segment.Status = status; segment.SegmentType = type; await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await controller.Ayah(segment.Id, default));
    }
    [Fact]
    public async Task UnapprovedFaceOrMissingFile_RefusesDownload()
    {
        segment.Wajh = new Wajh { Code = "test", NameArabic = "test", ReviewStatus = "Draft" }; await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await controller.Ayah(segment.Id, default));
        segment.Wajh.ReviewStatus = "Approved"; await db.SaveChangesAsync();
        File.Delete(Path.Combine(root,"published","test.mp3"));
        Assert.IsType<NotFoundResult>(await controller.Ayah(segment.Id, default));
    }
    [Fact]
    public async Task Surah_OnlyRegisteredOriginalAndValidNumber()
    {
        var result = Assert.IsType<PhysicalFileResult>(await controller.Surah(2, default));
        Assert.Equal("warsh-surah-002-original.mp3", result.FileDownloadName);
        Assert.True(result.EnableRangeProcessing);
        Assert.IsType<NotFoundResult>(await controller.Surah(3, default));
        Assert.IsType<BadRequestResult>(await controller.Surah(115, default));
        Assert.IsType<BadRequestResult>(await controller.SurahAvailability(0, default));
    }
    public void Dispose()
    {
        db.Dispose();
        File.Delete(Path.Combine(root,"published","test.mp3"));
        File.Delete(Path.Combine(root,"sources","002","original.mp3"));
        Directory.Delete(Path.Combine(root,"published"));
        Directory.Delete(Path.Combine(root,"sources","002"));
        Directory.Delete(Path.Combine(root,"sources"));
        Directory.Delete(root);
    }
    private class Storage(string root) : IStorageService
    {
        public string GetPhysicalPath(string key) => Path.Combine(root, key);
        public string GetPublicUrl(string key) => "/audio/" + key;
        public Task<bool> FileExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(File.Exists(GetPhysicalPath(key)));
        public Task<Stream?> GetAudioStreamAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> SaveAudioAsync(Stream content, string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
