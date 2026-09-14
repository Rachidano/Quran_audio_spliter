using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Quran.Web.Pages.Mushaf;

public class AudioReviewModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Admin/AudioEditor", new { surahNumber = 2 });
    }
}
