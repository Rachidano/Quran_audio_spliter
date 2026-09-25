using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.Interfaces;
using Quran.Infrastructure.Services;
using Quran.Infrastructure.Persistence;

namespace Quran.Web.Pages.Mushaf;

public class PageModelCustom(MarwPilotCorpus corpus, IStorageService storage, QuranDbContext db) : PageModel
{
    private static readonly Lazy<Dictionary<int, string>> Names = new(() =>
        System.Text.Json.JsonSerializer.Deserialize<List<SurahName>>(
            System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "App_Data", "surahs.json")))!
        .ToDictionary(s => s.Number, s => s.NameArabic));
    public string GetSurahName(int number) => Names.Value.GetValueOrDefault(number, number.ToString());
    private sealed record SurahName(int Number, string NameArabic);
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    // Legacy IDs deliberately do not resolve into the new edition.
    [BindProperty(SupportsGet = true)] public long? HighlightAyahId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Reference { get; set; }
    [BindProperty(SupportsGet = true)] public int? SurahNumber { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? PreviewPassage { get; set; }
    public WarshPilotPage MushafPage { get; private set; } = null!;
    public IReadOnlyList<int> AvailablePages => corpus.PageNumbers;
    public IEnumerable<int> NavigationPages => AvailablePages.Where(n => n == 1 || n == MarwPilotCorpus.PageCount || Math.Abs(n - PageNumber) <= 2);
    public IReadOnlyDictionary<int, int> SurahStartPages => corpus.SurahStartPages;
    public List<(int Surah, string Url)> SourceAudios { get; } = new();
    public string ImageUrl => $"/api/mushaf/marw-pilot/{MarwPilotCorpus.Revision}/pages/{PageNumber}/image.png?sha256={MushafPage.Sha256}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (PreviewPassage.HasValue)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Reviewer")) return Challenge();
            var passage = await db.AudioSegments.FindAsync([PreviewPassage.Value], cancellationToken);
            if (passage?.Reference == null || passage.Edition != MarwPilotCorpus.EditionCode) return NotFound();
            var target = corpus.PageNumbers.First(n => corpus.GetPage(n)!.Regions.Any(r => r.Reference == passage.Reference));
            if (SurahNumber.HasValue || PageNumber != target) return RedirectToPage("/Mushaf/Page", new { pageNumber = target, PreviewPassage, Reference = passage.Reference });
            Reference = passage.Reference;
        }
        if (SurahNumber.HasValue)
        {
            if (!SurahStartPages.TryGetValue(SurahNumber.Value, out var startPage)) return NotFound();
            return RedirectToPage("/Mushaf/Page", new { pageNumber = startPage, Reference = $"{SurahNumber}:1" });
        }
        if (corpus.GetPage(PageNumber) is not { } page) return NotFound();
        MushafPage = page;
        if (Reference != null && !page.Regions.Any(r => r.Reference == Reference)) return NotFound();
        if (User.Identity?.IsAuthenticated == true && (User.IsInRole("Admin") || User.IsInRole("Reviewer") || User.IsInRole("Annotator")))
        {
            foreach (var surah in page.Regions.Select(r => r.SurahNumber).Distinct())
            {
                var key = $"sources/{surah:D3}/original.mp3";
                if (await storage.FileExistsAsync(key, cancellationToken)) SourceAudios.Add((surah, storage.GetPublicUrl(key)));
            }
        }
        return Page();
    }
}
