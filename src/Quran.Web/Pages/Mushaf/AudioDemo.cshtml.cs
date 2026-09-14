using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Infrastructure.Services;

namespace Quran.Web.Pages.Mushaf;

public class AudioDemoModel(MarwPilotCorpus corpus) : PageModel
{
    public IEnumerable<WarshPilotPage> DemoPages => corpus.PageNumbers.Select(n => corpus.GetPage(n)!)
        .Where(p => p.Regions.Any(r => r.SurahNumber == 2 && r.AyahNumber <= 5));
    public void OnGet() { }
}
