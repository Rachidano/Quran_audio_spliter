using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages.Admin;

public class InventoryModel : PageModel
{
    private readonly IArchiveOrgInventoryService _inventoryService;

    public InventoryModel(IArchiveOrgInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public IReadOnlyList<ArchiveSourceItemDto> Items { get; set; } = Array.Empty<ArchiveSourceItemDto>();
    public ArchiveInventorySummaryDto? Summary { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Summary = await _inventoryService.GetSummaryAsync(cancellationToken);
        Items = await _inventoryService.GetLocalInventoryStatusAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSyncAsync(CancellationToken cancellationToken)
    {
        var count = await _inventoryService.SyncInventoryAsync(cancellationToken);
        StatusMessage = $"Synchronisation réussie : {count} fichiers sources catalogués depuis Archive.org.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDownloadAsync(int surahNumber, CancellationToken cancellationToken)
    {
        var success = await _inventoryService.DownloadSurahSourceAsync(surahNumber, cancellationToken);
        if (success)
        {
            StatusMessage = $"Sourate #{surahNumber:D3} téléchargée et vérifiée avec succès.";
        }
        else
        {
            StatusMessage = $"Erreur lors du téléchargement de la sourate #{surahNumber:D3}.";
        }

        return RedirectToPage();
    }
}
