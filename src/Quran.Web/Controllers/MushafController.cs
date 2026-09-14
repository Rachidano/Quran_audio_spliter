using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MushafController : ControllerBase
{
    private readonly IMushafImageService _mushafService;
    private readonly ILogger<MushafController> _logger;

    public MushafController(IMushafImageService mushafService, ILogger<MushafController> logger)
    {
        _mushafService = mushafService;
        _logger = logger;
    }

    [HttpGet("editions")]
    public async Task<ActionResult<MushafEditionDto>> GetDefaultEdition(CancellationToken cancellationToken)
    {
        var edition = await _mushafService.GetOrCreateDefaultEditionAsync(cancellationToken);
        return Ok(edition);
    }

    [HttpGet("pages/{pageNumber:int}")]
    [HttpGet("editions/{editionId:int}/pages/{pageNumber:int}")]
    public async Task<ActionResult<MushafPageViewerDto>> GetPage(int pageNumber, [FromRoute] int? editionId, CancellationToken cancellationToken)
    {
        try
        {
            var viewer = await _mushafService.GetPageViewerContextAsync(pageNumber, editionId, cancellationToken);
            return Ok(viewer);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Page non importée. Le pilote Warsh est disponible dans /mushaf/warsh/page/1." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de la page {Page}", pageNumber);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("ayahs/{ayahId:long}/regions")]
    public async Task<ActionResult<IReadOnlyList<AyahImageRegionDto>>> GetAyahRegions(long ayahId, CancellationToken cancellationToken)
    {
        var regions = await _mushafService.GetAyahRegionsAsync(ayahId, cancellationToken);
        return Ok(regions);
    }

    [HttpPost("/api/admin/mushaf/ayahs/{ayahId:long}/regions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AyahImageRegionDto>> CreateAyahRegion(long ayahId, [FromBody] CreateAyahRegionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var req = request with { AyahId = ayahId };
            var created = await _mushafService.CreateAyahRegionAsync(req, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création d'une région d'ayah");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPut("/api/admin/mushaf/ayah-regions/{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AyahImageRegionDto>> UpdateAyahRegion(long id, [FromBody] UpdateAyahRegionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _mushafService.UpdateAyahRegionAsync(id, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour d'une région {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpDelete("/api/admin/mushaf/ayah-regions/{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAyahRegion(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _mushafService.DeleteAyahRegionAsync(id, cancellationToken);
            return Ok(new { message = "Région supprimée avec succès." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression de la région {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("/api/admin/audio-segments/{segmentId:guid}/image-regions")]
    [HttpPost("/api/admin/mushaf/audio-segments/{segmentId:guid}/image-regions")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<ActionResult<AudioSegmentImageRegionDto>> CreateSegmentImageRegion(Guid segmentId, [FromBody] CreateSegmentImageRegionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var req = request with { AudioSegmentId = segmentId };
            var created = await _mushafService.CreateSegmentImageRegionAsync(req, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création d'une région de segment audio");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("/api/admin/audio-segments/{segmentId:guid}/image-regions")]
    [HttpGet("/api/admin/mushaf/audio-segments/{segmentId:guid}/image-regions")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<ActionResult<IReadOnlyList<AudioSegmentImageRegionDto>>> GetSegmentImageRegions(Guid segmentId, CancellationToken cancellationToken)
    {
        var regions = await _mushafService.GetSegmentImageRegionsAsync(segmentId, cancellationToken);
        return Ok(regions);
    }

    [HttpDelete("/api/admin/audio-segment-image-regions/{id:long}")]
    [HttpDelete("/api/admin/mushaf/audio-segment-image-regions/{id:long}")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<IActionResult> DeleteSegmentImageRegion(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _mushafService.DeleteSegmentImageRegionAsync(id, cancellationToken);
            return Ok(new { message = "Région de segment supprimée avec succès." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression de la région de segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
