using System.IO;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class QuranSearchTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly QuranSearchService _searchService;

    public QuranSearchTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_search_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _searchService = new QuranSearchService();
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Theory]
    [InlineData("2:255", 2, 255)]
    [InlineData("2-255", 2, 255)]
    [InlineData("2 255", 2, 255)]
    [InlineData("البقرة 255", 2, 255)]
    [InlineData("سورة الفاتحة 7", 1, 7)]
    public void TryParseReference_WithValidInputs_ExtractsSurahAndAyah(string query, int expectedSurah, int expectedAyah)
    {
        var success = QuranSearchService.TryParseReference(query, out var surah, out var ayah);

        Assert.True(success);
        Assert.Equal(expectedSurah, surah);
        Assert.Equal(expectedAyah, ayah);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("200:1")] // Out of range surah
    [InlineData("1:50")]  // Fatiha only has 7 ayahs
    public void TryParseReference_WithInvalidInputs_ReturnsFalse(string query)
    {
        var success = QuranSearchService.TryParseReference(query, out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void ArabicTextNormalizer_NormalizesTashkeelAlifAndYa()
    {
        var raw = "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ";
        var norm = ArabicTextNormalizer.Normalize(raw);

        Assert.Equal("بسم الله الرحمن الرحيم", norm);

        // NormalizeForSearch strips extra punctuation
        var searchNorm = ArabicTextNormalizer.NormalizeForSearch("أإآٱ");
        Assert.Equal("اااا", searchNorm);

        // Alif Maqsura
        Assert.Equal("علي", ArabicTextNormalizer.NormalizeForSearch("عَلَىٰ"));
    }

    [Fact]
    public void HasMinimumLetters_EnforcesThreeLetterThreshold()
    {
        Assert.False(ArabicTextNormalizer.HasMinimumLetters("قل")); // 2 letters
        Assert.False(ArabicTextNormalizer.HasMinimumLetters(" لا ")); // 2 letters
        Assert.True(ArabicTextNormalizer.HasMinimumLetters("كتب")); // 3 letters
        Assert.True(ArabicTextNormalizer.HasMinimumLetters("الله")); // 4 letters
    }

    [Fact]
    public async Task Search_OnRealCorpus_MatchesContiguousWordsAndExcludesDispersed()
    {
        var realPackDir = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full");
        var realQpcJson = Path.GetFullPath("data/reference/warsh-qpc-v10/source.json");

        if (!Directory.Exists(realPackDir) || !File.Exists(realQpcJson)) return;

        // Import corpus into test workspace
        var importer = new CorpusImportService(_workspaceService);
        await importer.ImportPackAsync(realPackDir, realQpcJson);

        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        await _searchService.InitializeAsync(dbPath);

        Assert.True(_searchService.IsInitialized);
        Assert.Equal(6214, _searchService.IndexedAyahsCount);

        // Test "الله لا" (as explicitly specified in L04-04)
        var result = _searchService.Search("الله لا", pageNumber: 1, pageSize: 50);

        Assert.True(result.TotalResults > 0);
        Assert.Null(result.ValidationMessage);

        // Every returned ayah must contain "الله لا" contiguously in its normalized text
        var targetNorm = ArabicTextNormalizer.NormalizeForSearch("الله لا");
        foreach (var item in result.Items)
        {
            Assert.Contains(targetNorm, item.NormalizedText);
        }

        // Specifically check Ayah 2:255 (Ayat al-Kursi: "الله لا اله الا هو...") is present
        Assert.Contains(result.Items, item => item.SurahNumber == 2 && item.AyahNumber == 255);

        // Test words with dagger alif: "العالمين" with explicit alif
        var resAlamin = _searchService.Search("العالمين");
        Assert.True(resAlamin.TotalResults > 0);
        Assert.Contains(resAlamin.Items, i => i.SurahNumber == 1 && i.AyahNumber == 1);

        // Test "الرحمن" without alif
        var resRahman = _searchService.Search("الرحمن");
        Assert.True(resRahman.TotalResults > 0);
        Assert.Contains(resRahman.Items, i => i.SurahNumber == 1 && i.AyahNumber == 2);

        // Test phrase "الحمد لله"
        var resHamd = _searchService.Search("الحمد لله");
        Assert.True(resHamd.TotalResults > 0);
        Assert.Contains(resHamd.Items, i => i.SurahNumber == 1 && i.AyahNumber == 1);
    }

    [Fact]
    public async Task Search_ShortQuery_ReturnsValidationMessageWithoutCrashing()
    {
        var realPackDir = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full");
        var realQpcJson = Path.GetFullPath("data/reference/warsh-qpc-v10/source.json");
        if (!Directory.Exists(realPackDir)) return;

        var importer = new CorpusImportService(_workspaceService);
        await importer.ImportPackAsync(realPackDir, realQpcJson);
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        await _searchService.InitializeAsync(dbPath);

        var result = _searchService.Search("قل", pageNumber: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.NotNull(result.ValidationMessage);
        Assert.Contains("3 أحرف على الأقل", result.ValidationMessage);
    }

    [Fact]
    public async Task Search_ByReference_FindsExactAyahWithoutThreeLetterRequirement()
    {
        var realPackDir = Path.GetFullPath("data/mushaf/editions/marw-newMushaf/20260913-full");
        var realQpcJson = Path.GetFullPath("data/reference/warsh-qpc-v10/source.json");
        if (!Directory.Exists(realPackDir)) return;

        var importer = new CorpusImportService(_workspaceService);
        await importer.ImportPackAsync(realPackDir, realQpcJson);
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        await _searchService.InitializeAsync(dbPath);

        var result = _searchService.Search("2:255");

        Assert.Single(result.Items);
        var ayah = result.Items[0];
        Assert.Equal(2, ayah.SurahNumber);
        Assert.Equal(255, ayah.AyahNumber);
        Assert.Equal("سورة البقرة (255)", ayah.FormattedReference);
    }

    [Fact]
    public void LibraryViewModel_HasSearchValidationMessage_ReflectsState()
    {
        var vm = new QuranVideo.Desktop.ViewModels.LibraryViewModel((_, _) => { });
        Assert.False(vm.HasSearchValidationMessage);

        vm.SearchValidationMessage = "أدخل 3 أحرف على الأقل للبحث بالكلمات";
        Assert.True(vm.HasSearchValidationMessage);

        vm.SearchValidationMessage = null;
        Assert.False(vm.HasSearchValidationMessage);

        vm.SearchValidationMessage = "";
        Assert.False(vm.HasSearchValidationMessage);
    }
}
