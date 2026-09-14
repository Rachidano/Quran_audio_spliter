using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class InventoryController : ControllerBase
{
    private readonly IArchiveOrgInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IArchiveOrgInventoryService inventoryService,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArchiveSourceItemDto>>> GetInventory(CancellationToken cancellationToken)
    {
        var items = await _inventoryService.GetLocalInventoryStatusAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ArchiveInventorySummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _inventoryService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncMetadata(CancellationToken cancellationToken)
    {
        var count = await _inventoryService.SyncInventoryAsync(cancellationToken);
        return Ok(new { message = $"Synchronisation terminée avec succès. {count} fichiers sources catalogués.", count });
    }

    [HttpPost("download/{surahNumber:int}")]
    public async Task<IActionResult> DownloadSurah(int surahNumber, CancellationToken cancellationToken)
    {
        if (surahNumber < 1 || surahNumber > 114)
            return BadRequest(new { message = "Numéro de sourate invalide (doit être entre 1 et 114)." });

        var success = await _inventoryService.DownloadSurahSourceAsync(surahNumber, cancellationToken);
        if (success)
        {
            return Ok(new { message = $"Téléchargement de la sourate #{surahNumber} réussi.", surahNumber });
        }

        return StatusCode(500, new { message = $"Échec du téléchargement pour la sourate #{surahNumber}." });
    }

    [HttpPost("download-batch")]
    public async Task<IActionResult> DownloadBatch([FromBody] List<int> surahNumbers, CancellationToken cancellationToken)
    {
        if (surahNumbers == null || surahNumbers.Count == 0)
            return BadRequest(new { message = "Aucun numéro de sourate spécifié." });

        var validNumbers = surahNumbers.Where(n => n >= 1 && n <= 114).Distinct().ToList();
        var downloaded = await _inventoryService.DownloadBatchAsync(validNumbers, cancellationToken);

        return Ok(new { message = $"{downloaded}/{validNumbers.Count} fichiers téléchargés avec succès.", downloaded, totalRequested = validNumbers.Count });
    }
}
