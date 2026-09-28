using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/audio-editor")]
[Authorize(Roles = "Reviewer,Admin")]
public class AudioEditorController : ControllerBase
{
    private readonly IAudioEditorService _editorService;
    private readonly IAnnotationService _annotationService;
    private readonly IAudioPeaksService _peaksService;
    private readonly ILogger<AudioEditorController> _logger;

    public AudioEditorController(
        IAudioEditorService editorService,
        IAnnotationService annotationService,
        IAudioPeaksService peaksService,
        ILogger<AudioEditorController> logger)
    {
        _editorService = editorService;
        _annotationService = annotationService;
        _peaksService = peaksService;
        _logger = logger;
    }

    [HttpGet("peaks/{surahNumber:int}")]
    public async Task<ActionResult<AudioPeaksDto>> GetPeaks(
        int surahNumber,
        [FromQuery] double? startSeconds,
        [FromQuery] double? durationSeconds,
        [FromQuery] int maxPoints = 2000,
        CancellationToken cancellationToken = default)
    {
        var peaks = await _peaksService.GetPeaksBySurahAsync(surahNumber, startSeconds, durationSeconds, maxPoints, cancellationToken);
        return Ok(peaks);
    }

    [HttpGet("marw-ayah/{surahNumber:int}/{ayahNumber:int}")]
    public IActionResult GetMarwAyahRegion(int surahNumber, int ayahNumber, [FromServices] Quran.Infrastructure.Services.MarwPilotCorpus corpus)
    {
        foreach (var pageNum in corpus.PageNumbers)
        {
            var page = corpus.GetPage(pageNum);
            if (page == null) continue;
            var reg = page.Regions.FirstOrDefault(r => r.SurahNumber == surahNumber && r.AyahNumber == ayahNumber);
            if (reg != null)
            {
                return Ok(new
                {
                    pageNumber = pageNum,
                    imageUrl = $"/api/mushaf/marw-pilot/{Quran.Infrastructure.Services.MarwPilotCorpus.Revision}/pages/{pageNum}/image.png",
                    viewBox = page.ViewBox,
                    width = page.Width,
                    height = page.Height,
                    pathData = reg.PathData,
                    reference = reg.Reference
                });
            }
        }
        return NotFound(new { message = $"لم يتم العثور على موضع الآية {surahNumber}:{ayahNumber} في مصحف مصدرنا الرسمي." });
    }

    [HttpPost("capture/end-ayah")]
    public async Task<ActionResult<CaptureEndAyahResult>> CaptureEndAyah([FromBody] CaptureEndAyahRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var result = await _annotationService.CaptureEndAyahAsync(request, userId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la capture de fin de verset");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("capture/repeat")]
    public async Task<ActionResult<AudioSegmentDto>> CaptureRepeat([FromBody] CaptureRepeatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var result = await _annotationService.CaptureRepeatAsync(request, userId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la capture de répétition");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("capture/undo")]
    public async Task<ActionResult<UndoCaptureResult>> UndoLastCapture([FromQuery] Guid sourceAudioId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var result = await _annotationService.UndoLastCaptureAsync(sourceAudioId, userId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'annulation de la capture");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("session/{sourceAudioId:guid}")]
    public async Task<ActionResult<AnnotationSessionDto>> GetSession(Guid sourceAudioId, CancellationToken cancellationToken)
    {
        var userId = User.Identity?.Name ?? "reviewer";
        var session = await _annotationService.GetOrCreateSessionAsync(userId, sourceAudioId, cancellationToken);
        return Ok(session);
    }

    [HttpPut("session")]
    public async Task<ActionResult<AnnotationSessionDto>> UpdateSession([FromBody] UpdateSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.Identity?.Name ?? "reviewer";
        var session = await _annotationService.UpdateSessionAsync(request, userId, cancellationToken);
        return Ok(session);
    }

    [HttpGet("occurrences/{sourceAudioId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AudioSegmentDto>>> GetOccurrences(Guid sourceAudioId, CancellationToken cancellationToken)
    {
        var list = await _annotationService.GetOccurrencesForSourceAsync(sourceAudioId, cancellationToken);
        return Ok(list);
    }

    [HttpGet("surah/{surahNumber:int}")]
    public async Task<ActionResult<AudioEditorContextDto>> GetSurahContext(int surahNumber,
        [FromServices] Quran.Infrastructure.Persistence.QuranDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var context = await _editorService.GetEditorContextBySurahAsync(surahNumber, cancellationToken);
            var references = await db.CorpusAyahs.AsNoTracking()
                .Where(a => a.Corpus!.Code == "marw-newMushaf" && a.SurahNumber == surahNumber)
                .OrderBy(a => a.AyahNumber).ToListAsync(cancellationToken);
            // Zero is not a historical AyahId: the client must save null for that FK.
            context = context with {
                AyahCount = references.Count,
                Ayahs = references.Select(a => new AyahEditorDto(0, context.SourceAudio.SurahId,
                    a.AyahNumber, a.NumberInQuran,
                    string.Empty,
                    new List<AyahWordDto>())).ToList()
            };
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

    [HttpPost("reset-surah/{surahNumber:int}")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<IActionResult> ResetSurah(int surahNumber, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            await _editorService.ResetSurahAsync(surahNumber, userId, cancellationToken);
            return Ok(new { success = true, message = $"تم مسح جميع مقاطع السورة {surahNumber} بنجاح والبدء من الصفر." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la réinitialisation de la sourate {SurahNumber}", surahNumber);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/submit-review")]
    public async Task<ActionResult<AudioSegmentDto>> SubmitForReview(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var segment = await _editorService.SubmitForReviewAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la soumission pour revue du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/approve")]
    public async Task<ActionResult<AudioSegmentDto>> ApproveSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var segment = await _editorService.ApproveSegmentAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'approbation du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/reject")]
    public async Task<ActionResult<AudioSegmentDto>> RejectSegment(Guid id, [FromBody] RejectSegmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "reviewer";
            var segment = await _editorService.RejectSegmentAsync(id, request.Reason, userId, cancellationToken);
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
            _logger.LogError(ex, "Erreur lors du rejet du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/render")]
    [Authorize(Roles = "Admin")]
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du rendu du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la publication du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("segments/{id:guid}/unpublish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AudioSegmentDto>> UnpublishSegment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.Identity?.Name ?? "admin";
            var segment = await _editorService.UnpublishSegmentAsync(id, userId, cancellationToken);
            return Ok(segment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du retrait de publication du segment {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("review-queue")]
    public async Task<ActionResult<IReadOnlyList<AudioSegmentDto>>> GetReviewQueue(
        [FromQuery] int? surahNumber,
        [FromQuery] Quran.Domain.Enums.AudioSegmentStatus? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await _editorService.GetReviewQueueAsync(surahNumber, status, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de la file de revue");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("surah/{surahNumber:int}/tokenize")]
    [Authorize(Roles = "Admin")]
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
