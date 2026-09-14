using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/awjuh")]
public class AwjuhController : ControllerBase
{
    private readonly QuranDbContext _db;
    private readonly IAwjuhService _awjuhService;

    public AwjuhController(QuranDbContext db, IAwjuhService awjuhService)
    {
        _db = db;
        _awjuhService = awjuhService;
    }

    public record Edit(string Code, string NameArabic, string RouteCode, string Description, string SourceCitation, string ReviewStatus, int Version);
    public record DuplicateRequest(string NewCode);

    [HttpGet("catalog")]
    [AllowAnonymous]
    public async Task<IActionResult> Catalog(CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(AwjuhCatalog.Read().GetRawText())!;
        var entries = new JsonArray();
        var rows = await _db.Awjuh
            .AsNoTracking()
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        foreach (var w in rows)
        {
            var e = JsonNode.Parse(w.DefinitionJson) as JsonObject ?? new JsonObject();
            e["id"] = w.Id;
            e["code"] = w.Code;
            e["nameArabic"] = w.NameArabic;
            e["routeCode"] = w.RouteCode;
            e["scope"] = w.Description;
            e["sourceCitation"] = w.SourceCitation;
            e["status"] = w.ReviewStatus;
            e["version"] = w.Version;
            
            // Populate components
            var compsObj = new JsonObject();
            foreach (var c in w.Components)
            {
                if (c.ValueNumeric.HasValue) compsObj[c.ComponentKey] = c.ValueNumeric.Value;
                else compsObj[c.ComponentKey] = c.ValueArabic;
            }
            e["components"] = compsObj;

            entries.Add(e);
        }

        root["entries"] = entries;
        root["revision"] = "database-v4";

        var routes = (JsonArray)root["routes"]!;
        var dbRoutes = await _db.TransmissionRoutes.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var r in dbRoutes)
        {
            if (!routes.Any(x => x!["code"]!.GetValue<string>() == r.Code))
            {
                routes.Add(new JsonObject
                {
                    ["code"] = r.Code,
                    ["name"] = r.NameArabic,
                    ["coverage"] = r.CoverageStatus
                });
            }
        }

        return Ok(root);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<WajhDto>>> Search([FromQuery] AwjuhSearchFilter filter, CancellationToken cancellationToken)
    {
        var results = await _awjuhService.SearchAsync(filter, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<WajhDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var w = await _awjuhService.GetByIdAsync(id, cancellationToken);
        if (w == null) return NotFound(new { message = $"الوجه برقم {id} غير موجود." });
        return Ok(w);
    }

    [HttpGet("{id:int}/usages")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<WajhUsageReportDto>> GetUsageReport(int id, CancellationToken cancellationToken)
    {
        try
        {
            var report = await _awjuhService.GetUsageReportAsync(id, cancellationToken);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("reader-available")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WajhDto>>> GetAvailableForReader([FromQuery] int? surahNumber, [FromQuery] string? reference, CancellationToken cancellationToken)
    {
        var result = await _awjuhService.GetAvailableAwjuhForReaderAsync(surahNumber, reference, cancellationToken);
        return Ok(result);
    }

    [HttpGet("editor-candidates")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<WajhDto>>> GetCandidatesForEditor([FromQuery] int? surahNumber, [FromQuery] string? reference, CancellationToken cancellationToken)
    {
        var result = await _awjuhService.GetCandidateAwjuhForEditorAsync(surahNumber, reference, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<IActionResult> Create([FromBody] CreateWajhDto input, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "admin";
            var result = await _awjuhService.CreateDraftAsync(input, userId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWajhDto input, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "admin";
            var result = await _awjuhService.UpdateAsync(id, input, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewWajhDto input, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "reviewer";
            var result = await _awjuhService.ReviewAsync(id, input, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/duplicate")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateRequest input, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "admin";
            var result = await _awjuhService.DuplicateAsync(id, input.NewCode, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int version, CancellationToken cancellationToken)
    {
        try
        {
            await _awjuhService.DeleteDraftAsync(id, version, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("routes")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<TransmissionRouteDto>>> GetRoutes(CancellationToken cancellationToken)
    {
        var routes = await _awjuhService.GetRoutesAsync(cancellationToken);
        return Ok(routes);
    }

    [HttpGet("sources")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<ReferenceSourceDto>>> GetSources(CancellationToken cancellationToken)
    {
        var sources = await _awjuhService.GetSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    [HttpGet("constraints")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<CombinationConstraintDto>>> GetConstraints([FromQuery] string? routeCode, CancellationToken cancellationToken)
    {
        var constraints = await _awjuhService.GetConstraintsAsync(routeCode, cancellationToken);
        return Ok(constraints);
    }

    [HttpGet("coverage-matrix")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<List<DocumentaryCoverageEntryDto>>> GetCoverageMatrix([FromQuery] string? routeCode, CancellationToken cancellationToken)
    {
        var matrix = await _awjuhService.GetCoverageMatrixAsync(routeCode, cancellationToken);
        return Ok(matrix);
    }

    [HttpPut("coverage-matrix/{id:int}")]
    [Authorize(Roles = "Admin,Reviewer")]
    public async Task<ActionResult<DocumentaryCoverageEntryDto>> UpdateCoverageEntry(int id, [FromBody] UpdateCoverageEntryDto input, CancellationToken cancellationToken)
    {
        if (id != input.Id) return BadRequest(new { message = "عدم تطابق المعرف." });
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "reviewer";
            var result = await _awjuhService.UpdateCoverageEntryAsync(input, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
