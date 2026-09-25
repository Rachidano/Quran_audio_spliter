using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quran.Infrastructure.Persistence;

namespace Quran.Web.Pages.Admin;

public class AudioEditorModel : PageModel
{
    private readonly QuranDbContext _db;

    public AudioEditorModel(QuranDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int SurahNumber { get; set; } = 2;

    public record SurahOption(int Number, string NameArabic, string NameLatin, int AyahCount);
    public List<SurahOption> Surahs { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Surahs = await _db.Surahs
            .OrderBy(s => s.Number)
            .Select(s => new SurahOption(s.Number, s.NameArabic, s.NameLatin, s.AyahCount))
            .ToListAsync();
        var counts = await _db.CorpusAyahs.Where(a => a.Corpus!.Code == "marw-newMushaf")
            .GroupBy(a => a.SurahNumber).Select(g => new { Number = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Number);
        Surahs = Surahs.Select(s => counts.TryGetValue(s.Number, out var count) ? s with { AyahCount = count.Count } : s).ToList();

        if (SurahNumber < 1 || SurahNumber > 114)
        {
            SurahNumber = 2;
        }

        return Page();
    }
}
