using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.Interfaces;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[AllowAnonymous]
public class AudioDownloadsController(QuranDbContext db, IStorageService storage) : ControllerBase
{
    [HttpGet("audio/downloads/ayah/{id:guid}")]
    public async Task<IActionResult> Ayah(Guid id, CancellationToken ct)
    {
        var segment = await db.AudioSegments.AsNoTracking().Include(s => s.PublishedAudioAsset)
            .FirstOrDefaultAsync(s => s.Id == id && s.Edition == MarwPilotCorpus.EditionCode
                && s.Status == AudioSegmentStatus.Published && s.SegmentType == AudioSegmentType.FullAyah
                && s.PublishedAudioAsset != null && (s.Wajh == null || s.Wajh.ReviewStatus == "Approved"), ct);
        if (segment?.Reference == null) return NotFound();
        var parts = segment.Reference.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var surah) || !int.TryParse(parts[1], out var ayah)
            || !await db.CorpusAyahs.AnyAsync(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode
                && a.SurahNumber == surah && a.AyahNumber == ayah, ct)) return NotFound();
        var path = ExistingFile(segment.PublishedAudioAsset!.StorageKey, "published");
        if (path == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(path, "audio/mpeg", $"warsh-{surah:D3}-{ayah:D3}-{id:N}.mp3", enableRangeProcessing: true);
    }

    [HttpGet("api/audio-downloads/surah/{number:int}")]
    public async Task<IActionResult> SurahAvailability(int number, CancellationToken ct)
    {
        if (number is < 1 or > 114) return BadRequest();
        var path = await SurahPath(number, ct);
        return Ok(new { available = path != null, downloadUrl = path == null ? null : $"/audio/downloads/surah/{number}" });
    }

    [HttpGet("audio/downloads/surah/{number:int}")]
    public async Task<IActionResult> Surah(int number, CancellationToken ct)
    {
        if (number is < 1 or > 114) return BadRequest();
        var path = await SurahPath(number, ct);
        if (path == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(path, "audio/mpeg", $"warsh-surah-{number:D3}-original.mp3", enableRangeProcessing: true);
    }

    private async Task<string?> SurahPath(int number, CancellationToken ct)
    {
        // Explicit public download of the imported full-surah original; no arbitrary source path.
        var key = $"sources/{number:D3}/original.mp3";
        if (!await db.SourceAudios.AnyAsync(s => s.Surah.Number == number && s.StorageKey == key, ct)) return null;
        return ExistingFile(key, "sources");
    }

    private string? ExistingFile(string key, string folder)
    {
        if (!key.StartsWith(folder + "/", StringComparison.Ordinal) || key.Contains("..") || key.Contains('\\') || key.Contains(':')) return null;
        var root = Path.GetFullPath(storage.GetPhysicalPath(folder)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(storage.GetPhysicalPath(key));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path) ? path : null;
    }
}
