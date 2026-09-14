using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/audio-editor")]
public class AudioEditorController : ControllerBase
{
    private readonly IAudioEditorService _editorService;
    private readonly ILogger<AudioEditorController> _logger;

    public AudioEditorController(IAudioEditorService editorService, ILogger<AudioEditorController> logger)
    {
        _editorService = editorService;
        _logger = logger;
    }

    [HttpGet("surah/{surahNumber:int}")]
    public async Task<ActionResult<AudioEditorContextDto>> GetSurahContext(int surahNumber, CancellationToken cancellationToken)
    {
        try
        {
            var context = await _editorService.GetEditorContextBySurahAsync(surahNumber, cancellationToken);
            return Ok(context);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement du contexte de la sourate {Surah}", surahNumber);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments")]
    public async Task<ActionResult<AudioSegmentDto>> CreateSegment([FromBody] CreateAudioSegmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.CreateSegmentAsync(request, userId, cancellationToken);
            return Ok(segment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création d'un segment");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPut("segments/{id:guid}")]
    public async Task<ActionResult<AudioSegmentDto>> UpdateSegment(Guid id, [FromBody] UpdateAudioSegmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.UpdateSegmentAsync(id, request, userId, cancellationToken);
            return Ok(segment);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpDelete("segments/{id:guid}")]
    public async Task<IActionResult> DeleteSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _editorService.DeleteSegmentAsync(id, cancellationToken);
            return Ok(new { message = "Segment supprimé." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/approve")]
    public async Task<ActionResult<AudioSegmentDto>> ApproveSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.ApproveSegmentAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'approbation du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/render")]
    public async Task<ActionResult<AudioSegmentDto>> RenderSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.RenderSegmentAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du rendu du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/publish")]
    public async Task<ActionResult<AudioSegmentDto>> PublishSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.PublishSegmentAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la publication du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("surah/{surahNumber:int}/tokenize")]
    public async Task<IActionResult> TokenizeSurah(int surahNumber, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _editorService.TokenizeAyahsAsync(surahNumber, cancellationToken);
            return Ok(new { message = $"{count} ayahs tokenisées avec succès." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la tokenisation de la sourate {Surah}", surahNumber);
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
