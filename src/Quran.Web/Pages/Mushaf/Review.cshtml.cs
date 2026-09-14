using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Infrastructure.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Quran.Web.Pages.Mushaf;

public class ReviewModel(MarwPilotCorpus corpus) : PageModel
{
    public string Revision => MarwPilotCorpus.Revision;
    public IActionResult OnGetReport() => new JsonResult(MarwOpeningReview.Current);
    public int FirstTextLine(int surah) => MarwOpeningReview.Current.Entries[surah - 1].FirstTextLine + 1;
    public List<Opening> Openings { get; } = new();
    public sealed record Opening(int Surah, string Name, WarshPilotPage Page, WarshPilotRegion Region, string Fingerprint);
    private sealed record SurahName(int Number, string NameArabic);
    public void OnGet()
    {
        var names = JsonSerializer.Deserialize<List<SurahName>>(System.IO.File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "App_Data", "surahs.json")))!.ToDictionary(n => n.Number, n => n.NameArabic);
        foreach (var (surah, number) in corpus.SurahStartPages.OrderBy(p => p.Key))
        {
            var page = corpus.GetPage(number)!;
            var region = page.Regions.Single(r => r.SurahNumber == surah && r.AyahNumber == 1);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(page.Sha256 + region.PathData)));
            Openings.Add(new(surah, names[surah], page, region, fingerprint));
        }
    }
}
