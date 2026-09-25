using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Infrastructure.Services;
namespace Quran.Web.Pages;
public class SurahModel(MarwPilotCorpus corpus) : PageModel
{
    public IActionResult OnGet(int number = 1) => corpus.SurahStartPages.TryGetValue(number, out var page)
        ? Redirect($"/mushaf/warsh/page/{page}?Reference={number}:1") : NotFound();
}
