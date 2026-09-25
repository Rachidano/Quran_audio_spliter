using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.Interfaces;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Web.Controllers;

[ApiController]
[AllowAnonymous]
public class PublishedAudioController(QuranDbContext db, IStorageService storage) : ControllerBase
{
    [HttpGet("audio/published/{**filePath}")]
    public async Task<IActionResult> Get(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains("..") || filePath.Contains('\\') || filePath.Contains(':'))
            return BadRequest();
        var key = "published/" + filePath;
        var published = await db.AudioSegments.AnyAsync(s => s.Edition == Quran.Infrastructure.Services.MarwPilotCorpus.EditionCode && s.Status == AudioSegmentStatus.Published
            && s.PublishedAudioAsset != null && s.PublishedAudioAsset.StorageKey == key
            && (s.Wajh == null || s.Wajh.ReviewStatus == "Approved"), ct);
        if (!published) return NotFound();
        var path = Path.GetFullPath(storage.GetPhysicalPath(key));
        var root = Path.GetFullPath(storage.GetPhysicalPath("published")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(path, "audio/mpeg", enableRangeProcessing: true);
    }
}
