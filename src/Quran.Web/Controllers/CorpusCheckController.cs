using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class CorpusCheckController : ControllerBase
{
    private readonly ICorpusCheckerService _corpusCheckerService;
    private readonly IAdminValidationService _validationService;

    public CorpusCheckController(
        ICorpusCheckerService corpusCheckerService,
        IAdminValidationService validationService)
    {
        _corpusCheckerService = corpusCheckerService;
        _validationService = validationService;
    }

    [HttpGet]
    public async Task<ActionResult<CorpusCheckReport>> Check(CancellationToken cancellationToken)
    {
        var report = await _corpusCheckerService.CheckCorpusAsync(cancellationToken);
        return Ok(report);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> Dashboard(CancellationToken cancellationToken)
    {
        var stats = await _validationService.GetDashboardStatsAsync(cancellationToken);
        return Ok(stats);
    }
}
