using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.Interfaces;
using Quran.Infrastructure.Services;
using System.Security.Cryptography;

namespace Quran.Web.Pages.Mushaf;

public class AudioReviewModel(IStorageService storage, MarwPilotCorpus corpus) : PageModel
{
    public string AudioUrl => storage.GetPublicUrl("sources/002/original.mp3");
    public string SourceHash { get; private set; } = "";
    public string Edition => MarwPilotCorpus.EditionCode;
    public IReadOnlyList<int> Ayahs => corpus.PageNumbers.SelectMany(n => corpus.GetPage(n)!.Regions)
        .Where(r => r.SurahNumber == 2).Select(r => r.AyahNumber).Distinct().Order().ToArray();
    public IActionResult OnGet()
    {
        var path = storage.GetPhysicalPath("sources/002/original.mp3");
        if (!System.IO.File.Exists(path)) return NotFound();
        using var stream = System.IO.File.OpenRead(path);
        SourceHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return Page();
    }
}
