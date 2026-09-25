using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Infrastructure.Services;
namespace Quran.Web.Pages;
public class AyahModel(MarwPilotCorpus corpus) : PageModel
{
    public IActionResult OnGet(int surah, int ayah, string? wajh)
    {
        var reference = $"{surah}:{ayah}";
        var page = corpus.PageNumbers.FirstOrDefault(n => corpus.GetPage(n)!.Regions.Any(r => r.Reference == reference));
        if (page == 0) return NotFound();
        var url = $"/mushaf/warsh/page/{page}?Reference={reference}";
        if (!string.IsNullOrEmpty(wajh)) url += "&wajh=" + Uri.EscapeDataString(wajh);
        return Redirect(url);
    }
}
