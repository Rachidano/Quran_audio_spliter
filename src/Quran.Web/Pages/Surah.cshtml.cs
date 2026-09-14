using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages;

public class SurahModel : PageModel
{
    private readonly IQuranReaderService _quranReaderService;

    public SurahModel(IQuranReaderService quranReaderService)
    {
        _quranReaderService = quranReaderService;
    }

    [BindProperty(SupportsGet = true)]
    public int Number { get; set; } = 1;

    public SurahDto? Surah { get; set; }
    public IReadOnlyList<AyahDto> Ayahs { get; set; } = Array.Empty<AyahDto>();

    public async Task<IActionResult> OnGetAsync(int? number, CancellationToken cancellationToken)
    {
        var targetNumber = number ?? Number;
        if (targetNumber < 1 || targetNumber > 114)
            return RedirectToPage("/Index");

        Number = targetNumber;
        Surah = await _quranReaderService.GetSurahByNumberAsync(Number, cancellationToken);

        if (Surah == null)
            return NotFound();

        Ayahs = await _quranReaderService.GetAyahsBySurahAsync(Number, cancellationToken);
        return Page();
    }
}
