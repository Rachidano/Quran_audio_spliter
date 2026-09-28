using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Application.Common;

namespace Quran.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IQuranReaderService _quranReaderService;

    public IndexModel(IQuranReaderService quranReaderService)
    {
        _quranReaderService = quranReaderService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ResultPage { get; set; } = 1;
    public const int ResultsPerPage = 20;
    public int TotalResults { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalResults / (double)ResultsPerPage);
    public string? SearchError { get; private set; }

    public IReadOnlyList<SurahDto> Surahs { get; set; } = Array.Empty<SurahDto>();
    public IReadOnlyList<SearchResultDto> SearchResults { get; set; } = Array.Empty<SearchResultDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Q))
        {
            if (Q.Length > 200) SearchError = "يجب ألا يتجاوز البحث 200 حرف.";
            else if (!QuranSearchQuery.TryParseReference(Q, out _, out _) && !QuranSearchQuery.HasMinimumLetters(Q))
                SearchError = "أدخل ثلاثة أحرف على الأقل للبحث النصي، أو رقم السورة والآية مثل 2:4.";
            else
            {
                var allResults = await _quranReaderService.SearchAsync(Q, cancellationToken: cancellationToken);
                TotalResults = allResults.Count;
                ResultPage = Math.Clamp(ResultPage, 1, Math.Max(1, TotalPages));
                SearchResults = allResults.Skip((ResultPage - 1) * ResultsPerPage).Take(ResultsPerPage).ToList();
            }
        }
        
        Surahs = await _quranReaderService.GetAllSurahsAsync(cancellationToken);
    }
}
