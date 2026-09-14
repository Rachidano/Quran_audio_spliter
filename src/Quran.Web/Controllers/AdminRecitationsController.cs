using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Enums;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
public class RecitationsController : ControllerBase
{
    private readonly IAdminValidationService _validationService;

    public RecitationsController(IAdminValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRecitationItemDto>>> GetList(
        [FromQuery] int? surah,
        [FromQuery] RecitationStatus? status,
        CancellationToken cancellationToken)
    {
        var list = await _validationService.GetRecitationsForValidationAsync(surah, status, cancellationToken);
        return Ok(list);
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ActionCommentModel? model, CancellationToken cancellationToken)
    {
        await _validationService.ApproveRecitationAsync(id, model?.Comment, User.Identity?.Name ?? "admin", cancellationToken);
        return Ok(new { message = $"Récitation #{id} approuvée." });
    }

    [HttpPost("{id:long}/publish")]
    public async Task<IActionResult> Publish(long id, [FromBody] ActionCommentModel? model, CancellationToken cancellationToken)
    {
        await _validationService.PublishRecitationAsync(id, model?.Comment, User.Identity?.Name ?? "admin", cancellationToken);
        return Ok(new { message = $"Récitation #{id} publiée." });
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ActionCommentModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model?.Comment))
            return BadRequest(new { message = "Un motif de rejet est obligatoire." });

        await _validationService.RejectRecitationAsync(id, model.Comment, User.Identity?.Name ?? "admin", cancellationToken);
        return Ok(new { message = $"Récitation #{id} rejetée." });
    }

    [HttpPost("{id:long}/wajh")]
    public async Task<IActionResult> UpdateWajh(long id, [FromBody] UpdateWajhModel model, CancellationToken cancellationToken)
    {
        await _validationService.UpdateWajhAsync(id, model.WajhId, cancellationToken);
        return Ok(new { message = $"Wajh de la récitation #{id} mis à jour." });
    }

    public record ActionCommentModel(string? Comment);
    public record UpdateWajhModel(int WajhId);
}
