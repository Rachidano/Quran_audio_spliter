using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly IAdminValidationService _validationService;
    private readonly ICorpusCheckerService _corpusCheckerService;

    public IndexModel(
        IAdminValidationService validationService,
        ICorpusCheckerService corpusCheckerService)
    {
        _validationService = validationService;
        _corpusCheckerService = corpusCheckerService;
    }

    public AdminDashboardDto? DashboardStats { get; set; }
    public CorpusCheckReport? CorpusReport { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        DashboardStats = await _validationService.GetDashboardStatsAsync(cancellationToken);
        CorpusReport = await _corpusCheckerService.CheckCorpusAsync(cancellationToken);
    }
}
