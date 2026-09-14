using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages.Admin;

[Authorize(Roles = "Admin,Reviewer")]
public class AwjuhModel : PageModel
{
    private readonly IAwjuhService _awjuhService;

    public AwjuhModel(IAwjuhService awjuhService)
    {
        _awjuhService = awjuhService;
    }

    [BindProperty(SupportsGet = true)]
    public string? RouteCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RuleGroup { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public List<WajhDto> AwjuhList { get; set; } = new();
    public List<TransmissionRouteDto> Routes { get; set; } = new();
    public List<ReferenceSourceDto> Sources { get; set; } = new();
    public List<CombinationConstraintDto> Constraints { get; set; } = new();
    public List<DocumentaryCoverageEntryDto> CoverageMatrix { get; set; } = new();

    public string? FeedbackMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Ensure initial seeds are safely present
        await _awjuhService.EnsureSeedDataAsync(force: false, cancellationToken);

        Routes = await _awjuhService.GetRoutesAsync(cancellationToken);
        Sources = await _awjuhService.GetSourcesAsync(cancellationToken);
        Constraints = await _awjuhService.GetConstraintsAsync(RouteCode, cancellationToken);
        CoverageMatrix = await _awjuhService.GetCoverageMatrixAsync(RouteCode, cancellationToken);

        var filter = new AwjuhSearchFilter(
            RouteCode: RouteCode,
            RuleGroup: RuleGroup,
            Status: Status,
            Query: Query
        );

        AwjuhList = await _awjuhService.SearchAsync(filter, cancellationToken);
    }

    public async Task<IActionResult> OnPostReviewAsync(int id, string action, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "reviewer";
            await _awjuhService.ReviewAsync(id, new ReviewWajhDto(action, reason), userId, cancellationToken);
            TempData["Success"] = $"تم تنفيذ الإجراء '{action}' بنجاح.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { RouteCode, Status, RuleGroup, Query });
    }

    public async Task<IActionResult> OnPostUpdateCoverageAsync(int id, string status, string? notes, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "reviewer";
            await _awjuhService.UpdateCoverageEntryAsync(new UpdateCoverageEntryDto(id, status, notes), userId, cancellationToken);
            TempData["Success"] = "تم تحديث حالة التغطية التوثيقية بنجاح.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { RouteCode, Status, RuleGroup, Query });
    }
}
