using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

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

    public IReadOnlyList<SurahDto> Surahs { get; set; } = Array.Empty<SurahDto>();
    public IReadOnlyList<SearchResultDto> SearchResults { get; set; } = Array.Empty<SearchResultDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Q))
        {
            SearchResults = await _quranReaderService.SearchAsync(Q, 20, cancellationToken);
        }
        
        Surahs = await _quranReaderService.GetAllSurahsAsync(cancellationToken);
    }
}
