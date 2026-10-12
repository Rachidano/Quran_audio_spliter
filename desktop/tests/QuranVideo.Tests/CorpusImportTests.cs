using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class CorpusImportTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly string _mockPackDir;
    private readonly WorkspaceService _workspaceService;

    public CorpusImportTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_import_test_{Guid.NewGuid():N}");
        _mockPackDir = Path.Combine(Path.GetTempPath(), $"quran_mock_pack_{Guid.NewGuid():N}");

        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        CreateMockPack(_mockPackDir);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
            if (Directory.Exists(_mockPackDir)) Directory.Delete(_mockPackDir, true);
        }
        catch { }
    }

    private static void CreateMockPack(string packDir)
    {
        Directory.CreateDirectory(packDir);

        // Manifest
        var manifest = new
        {
            edition = "newMushaf",
            revision = "20260913-test",
            sourceUrl = "https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1",
            totalPages = 2,
            width = 1241,
            height = 1754
        };
        File.WriteAllText(Path.Combine(packDir, "manifest.json"), JsonSerializer.Serialize(manifest));

        // Create dummy PNG files
        File.WriteAllBytes(Path.Combine(packDir, "page-001.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        File.WriteAllBytes(Path.Combine(packDir, "page-002.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // Page 1 JSON (Surah 1: Ayahs 1 to 7)
        var page1 = new
        {
            page = 1,
            cropBox = new { left = 10.0, top = 10.0, width = 80.0, height = 80.0 },
            textBox = new { left = 5.0, top = 5.0, width = 90.0, height = 90.0 },
            rows = new[]
            {
                new
                {
                    surah = 1, ayah = 1, verseNo = 1,
                    rects = new[] { new { left = 20.0, top = 20.0, width = 60.0, height = 10.0 } }
                },
                new
                {
                    surah = 1, ayah = 2, verseNo = 2,
                    rects = new[] { new { left = 15.0, top = 35.0, width = 70.0, height = 10.0 } }
                }
            }
        };
        File.WriteAllText(Path.Combine(packDir, "page-001.json"), JsonSerializer.Serialize(page1));

        // Page 2 JSON (Surah 2: Ayah 1)
        var page2 = new
        {
            page = 2,
            cropBox = new { left = 10.0, top = 10.0, width = 80.0, height = 80.0 },
            textBox = new { left = 5.0, top = 5.0, width = 90.0, height = 90.0 },
            rows = new[]
            {
                new
                {
                    surah = 2, ayah = 1, verseNo = 8,
                    rects = new[] { new { left = 25.0, top = 20.0, width = 50.0, height = 10.0 } }
                }
            }
        };
        File.WriteAllText(Path.Combine(packDir, "page-002.json"), JsonSerializer.Serialize(page2));
    }

    [Fact]
    public async Task ImportPackAsync_ImportsEntitiesAndComputesPixelBoundsCorrectly()
    {
        var importer = new CorpusImportService(_workspaceService);
        var (success, error) = await importer.ImportPackAsync(_mockPackDir);

        Assert.True(success, error);
        Assert.Null(error);

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        // Verify edition
        var edition = await db.Editions.FirstOrDefaultAsync(e => e.Code == "newMushaf");
        Assert.NotNull(edition);
        Assert.Equal("20260913-test", edition.Revision);

        // Verify Surahs
        var surahsCount = await db.Surahs.CountAsync();
        Assert.Equal(114, surahsCount);

        // Verify Pages
        var pagesCount = await db.Pages.CountAsync();
        Assert.Equal(2, pagesCount);

        // Verify Ayahs
        var ayahs = await db.Ayahs.OrderBy(a => a.VerseNo).ToListAsync();
        Assert.Equal(3, ayahs.Count);
        Assert.Equal("1:1", ayahs[0].Id);
        Assert.Equal("1:2", ayahs[1].Id);
        Assert.Equal("2:1", ayahs[2].Id);

        // Verify Regions and Pixel Coordinates
        var regions = await db.Regions.ToListAsync();
        Assert.Equal(3, regions.Count);
        foreach (var r in regions)
        {
            Assert.True(r.PixelWidth > 0);
            Assert.True(r.PixelHeight > 0);
            Assert.True(r.PixelX >= 0);
            Assert.True(r.PixelY >= 0);
        }
    }

    [Fact]
    public async Task ImportPackAsync_IsIdempotent_SecondCallSucceedsWithoutDuplicateErrors()
    {
        var importer = new CorpusImportService(_workspaceService);

        var (success1, _) = await importer.ImportPackAsync(_mockPackDir);
        Assert.True(success1);

        // Second import of same revision
        var (success2, error2) = await importer.ImportPackAsync(_mockPackDir);
        Assert.True(success2, error2);

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        // Counts should remain unchanged
        Assert.Equal(1, await db.Editions.CountAsync());
        Assert.Equal(2, await db.Pages.CountAsync());
        Assert.Equal(3, await db.Ayahs.CountAsync());
    }

    [Fact]
    public async Task ImportPackAsync_WhenDirectoryMissing_ReturnsClearArabicError()
    {
        var importer = new CorpusImportService(_workspaceService);
        var (success, error) = await importer.ImportPackAsync("D:\\NonExistentDirectory_12345");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("غير موجود", error);
    }

    [Fact]
    public async Task ImportPackAsync_WithRealCorpus_ImportsAll604PagesAnd6214Ayahs()
    {
        var realPackDir = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full");
        var realQpcJson = Path.GetFullPath("data/reference/warsh-qpc-v10/source.json");
        var realOpeningJson = Path.GetFullPath("desktop/doc/validation/L00-05_corrections_ouvertures.md"); // or review json

        if (!Directory.Exists(realPackDir)) return;

        var importer = new CorpusImportService(_workspaceService);
        var (success, error) = await importer.ImportPackAsync(realPackDir, realQpcJson, null);

        Assert.True(success, error);
        Assert.Null(error);

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);

        // Verify total pages: 604
        var pageCount = await db.Pages.CountAsync();
        Assert.Equal(604, pageCount);

        // Verify total surahs: 114
        var surahCount = await db.Surahs.CountAsync();
        Assert.Equal(114, surahCount);

        // Verify total ayahs: 6,214
        var ayahCount = await db.Ayahs.CountAsync();
        Assert.Equal(6214, ayahCount);

        // Verify Baqara has exactly 285 ayahs in Warsh
        var baqara = await db.Surahs.FirstOrDefaultAsync(s => s.Number == 2);
        Assert.NotNull(baqara);
        Assert.Equal(285, baqara.AyahCount);

        var baqaraAyahsCount = await db.Ayahs.CountAsync(a => a.SurahNumber == 2);
        Assert.Equal(285, baqaraAyahsCount);

        // Verify QPC texts imported
        var textCount = await db.Texts.CountAsync();
        Assert.Equal(6214, textCount);
    }
}
