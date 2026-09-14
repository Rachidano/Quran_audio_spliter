using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages;

public class AyahModel : PageModel
{
    private readonly IQuranReaderService _quranReaderService;
    private readonly IMushafImageService _mushafService;

    public AyahModel(IQuranReaderService quranReaderService, IMushafImageService mushafService)
    {
        _quranReaderService = quranReaderService;
        _mushafService = mushafService;
    }

    [BindProperty(SupportsGet = true)]
    public int Surah { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int Ayah { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Wajh { get; set; }

    public SurahDto? SurahInfo { get; set; }
    public AyahDto? AyahInfo { get; set; }
    public RecitationDto? SelectedRecitation { get; set; }
    public IReadOnlyList<AyahImageRegionDto> MushafRegions { get; set; } = new List<AyahImageRegionDto>();
    public MushafPageDto? MushafPage { get; set; }

    public int? PrevAyahNumber => Ayah > 1 ? Ayah - 1 : null;
    public int? NextAyahNumber => (SurahInfo != null && Ayah < SurahInfo.AyahCount) ? Ayah + 1 : null;

    public async Task<IActionResult> OnGetAsync(int? surah, int? ayah, CancellationToken cancellationToken)
    {
        if (surah.HasValue) Surah = surah.Value;
        if (ayah.HasValue) Ayah = ayah.Value;

        SurahInfo = await _quranReaderService.GetSurahByNumberAsync(Surah, cancellationToken);
        if (SurahInfo == null)
            return NotFound();

        AyahInfo = await _quranReaderService.GetAyahAsync(Surah, Ayah, cancellationToken);
        if (AyahInfo == null)
            return NotFound();

        if (!string.IsNullOrEmpty(Wajh))
        {
            SelectedRecitation = AyahInfo.Recitations.FirstOrDefault(r => string.Equals(r.WajhCode, Wajh, StringComparison.OrdinalIgnoreCase));
        }
        SelectedRecitation ??= AyahInfo.Recitations.FirstOrDefault();

        MushafRegions = await _mushafService.GetAyahRegionsAsync(AyahInfo.Id, cancellationToken);
        if (MushafRegions.Count > 0)
        {
            var pageNum = MushafRegions[0].PageNumber;
            MushafPage = await _mushafService.GetPageAsync(pageNum, null, cancellationToken);
        }

        return Page();
    }
}
