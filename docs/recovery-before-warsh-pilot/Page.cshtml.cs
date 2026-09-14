using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quran.Infrastructure.Persistence;

namespace Quran.Web.Pages.Mushaf;

public class PageModelCustom : PageModel
{
    private readonly QuranDbContext _db;

    public PageModelCustom(QuranDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public long? HighlightAyahId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > 604) PageNumber = 604;

        if (HighlightAyahId.HasValue)
        {
            var ayah = await _db.Ayahs.FirstOrDefaultAsync(a => a.Id == HighlightAyahId.Value);
            if (ayah?.PageNumber != null)
            {
                PageNumber = ayah.PageNumber.Value;
            }
        }

        return Page();
    }
}
