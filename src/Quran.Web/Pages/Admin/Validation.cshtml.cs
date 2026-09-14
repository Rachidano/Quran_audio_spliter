using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;

namespace Quran.Web.Pages.Admin;

public class ValidationModel : PageModel
{
    private readonly IAdminValidationService _validationService;

    public ValidationModel(IAdminValidationService validationService)
    {
        _validationService = validationService;
    }

    [BindProperty(SupportsGet = true)]
    public int? Surah { get; set; }

    [BindProperty(SupportsGet = true)]
    public RecitationStatus? Status { get; set; }

    public IReadOnlyList<AdminRecitationItemDto> Recitations { get; set; } = Array.Empty<AdminRecitationItemDto>();
    public IReadOnlyList<Wajh> AvailableAwjuh { get; set; } = Array.Empty<Wajh>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        AvailableAwjuh = await _validationService.GetAvailableAwjuhAsync(cancellationToken);
        Recitations = await _validationService.GetRecitationsForValidationAsync(Surah, Status, cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(long recitationId, string? comment, CancellationToken cancellationToken)
    {
        await _validationService.ApproveRecitationAsync(recitationId, comment, User.Identity?.Name ?? "admin", cancellationToken);
        return RedirectToPage(new { Surah, Status });
    }

    public async Task<IActionResult> OnPostPublishAsync(long recitationId, string? comment, CancellationToken cancellationToken)
    {
        await _validationService.PublishRecitationAsync(recitationId, comment, User.Identity?.Name ?? "admin", cancellationToken);
        return RedirectToPage(new { Surah, Status });
    }

    public async Task<IActionResult> OnPostRejectAsync(long recitationId, string comment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            ModelState.AddModelError("Comment", "Le motif de rejet est obligatoire.");
            await OnGetAsync(cancellationToken);
            return Page();
        }

        await _validationService.RejectRecitationAsync(recitationId, comment, User.Identity?.Name ?? "admin", cancellationToken);
        return RedirectToPage(new { Surah, Status });
    }

    public async Task<IActionResult> OnPostUpdateWajhAsync(long recitationId, int wajhId, CancellationToken cancellationToken)
    {
        await _validationService.UpdateWajhAsync(recitationId, wajhId, cancellationToken);
        return RedirectToPage(new { Surah, Status });
    }
}
