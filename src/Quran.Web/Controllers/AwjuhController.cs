using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/awjuh")]
public class AwjuhController(QuranDbContext db) : ControllerBase
{
    public record Edit(string Code, string NameArabic, string RouteCode, string Description, string SourceCitation, string ReviewStatus, int Version);
    [HttpGet("catalog")]
    [AllowAnonymous]
    public async Task<IActionResult> Catalog()
    {
        var root = JsonNode.Parse(AwjuhCatalog.Read().GetRawText())!;
        var entries = new JsonArray();
        var rows = await db.Awjuh.AsNoTracking().OrderBy(w => w.SortOrder).ThenBy(w => w.Id).ToListAsync();
        foreach (var w in rows)
        {
            var e = JsonNode.Parse(w.DefinitionJson) as JsonObject ?? new JsonObject();
            e["id"] = w.Id; e["code"] = w.Code; e["nameArabic"] = w.NameArabic; e["routeCode"] = w.RouteCode;
            e["scope"] = w.Description; e["sourceCitation"] = w.SourceCitation; e["status"] = w.ReviewStatus; e["version"] = w.Version;
            e["components"] ??= new JsonObject(); e["sourceIds"] ??= new JsonArray();
            entries.Add(e);
        }
        root["entries"] = entries; root["revision"] = "database";
        var routes = (JsonArray)root["routes"]!;
        foreach (var code in rows.Select(w => w.RouteCode).Distinct())
            if (!routes.Any(r => r!["code"]!.GetValue<string>() == code)) routes.Add(new JsonObject { ["code"] = code, ["name"] = code, ["coverage"] = "Partial" });
        return Ok(root);
    }
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Edit input)
    {
        if (Validate(input) is { } error) return BadRequest(new { message = error });
        if (!System.Text.RegularExpressions.Regex.IsMatch(input.Code ?? "", "^[a-z0-9-]{3,50}$")) return BadRequest(new { message = "الرمز: حروف لاتينية صغيرة وأرقام وشرطة، من 3 إلى 50." });
        if (await db.Awjuh.AnyAsync(w => w.Code == input.Code)) return Conflict(new { message = "الرمز مستخدم." });
        var row = new Wajh { Code = input.Code, NameArabic = input.NameArabic.Trim(), RouteCode = input.RouteCode.Trim(), Description = input.Description.Trim(), SourceCitation = input.SourceCitation.Trim(), ReviewStatus = input.ReviewStatus };
        db.Awjuh.Add(row); await db.SaveChangesAsync(); return Ok(new { row.Id, row.Version });
    }
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, Edit input)
    {
        if (Validate(input) is { } error) return BadRequest(new { message = error });
        var row = await db.Awjuh.FindAsync(id); if (row == null) return NotFound();
        if (input.Version != row.Version) return Conflict(new { message = "تغير الوجه في جلسة أخرى. أعد التحميل." });
        if (input.Code != row.Code) return BadRequest(new { message = "الرمز ثابت حفاظًا على الروابط." });
        row.NameArabic = input.NameArabic.Trim(); row.RouteCode = input.RouteCode.Trim(); row.Description = input.Description.Trim();
        row.SourceCitation = input.SourceCitation.Trim(); row.ReviewStatus = input.ReviewStatus; row.Version++;
        // Any edit invalidates prior use; re-review the occurrence against the revised definition.
        foreach (var p in await db.MushafPassages.Where(p => p.WajhId == id).ToListAsync()) { p.Status = "Draft"; p.Version++; }
        foreach (var r in await db.Recitations.Where(r => r.WajhId == id).ToListAsync()) { r.IsPublished = false; r.Status = RecitationStatus.Imported; }
        foreach (var s in await db.AudioSegments.Where(s => s.WajhId == id).ToListAsync()) { s.Status = AudioSegmentStatus.Draft; s.ValidatedAt = null; s.ValidatedByUserId = null; s.PublishedAudioAssetId = null; }
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(new { message = "تعارض تعديل. أعد التحميل." }); }
        return Ok(new { row.Id, row.Version });
    }
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int version)
    {
        var row = await db.Awjuh.FindAsync(id); if (row == null) return NotFound();
        if (version != row.Version) return Conflict(new { message = "أعد تحميل المرجع قبل الحذف." });
        if (await db.MushafPassages.AnyAsync(p => p.WajhId == id) || await db.AudioSegments.AnyAsync(p => p.WajhId == id) || await db.Recitations.AnyAsync(p => p.WajhId == id))
            return Conflict(new { message = "الوجه مرتبط بمقاطع. اختر حالة مؤرشف بدل الحذف." });
        db.Remove(row); await db.SaveChangesAsync(); return NoContent();
    }
    private static string? Validate(Edit i) => string.IsNullOrWhiteSpace(i.NameArabic) || i.NameArabic.Length > 150 || string.IsNullOrWhiteSpace(i.RouteCode) || i.RouteCode.Length > 100 || string.IsNullOrWhiteSpace(i.Description) || i.Description.Length > 500 || string.IsNullOrWhiteSpace(i.SourceCitation) || i.SourceCitation.Length > 2000 || !new[] { "PendingReview", "Approved", "Archived" }.Contains(i.ReviewStatus)
        ? "الاسم والطريق والوصف والمصدر مطلوبة. تحقق من أطوال الحقول والحالة." : null;
}
