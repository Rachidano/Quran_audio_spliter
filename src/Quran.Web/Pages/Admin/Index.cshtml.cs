using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
namespace Quran.Web.Pages.Admin;
public class IndexModel(IAdminValidationService validationService) : PageModel
{
    public AdminDashboardDto? DashboardStats { get; private set; }
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        DashboardStats = await validationService.GetDashboardStatsAsync(cancellationToken);
}
