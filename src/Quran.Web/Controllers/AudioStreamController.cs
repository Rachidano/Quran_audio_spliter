using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("audio")]
[Authorize(Roles = "Reviewer,Admin")]
public class AudioStreamController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<AudioStreamController> _logger;

    public AudioStreamController(IStorageService storageService, ILogger<AudioStreamController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet("sources/{*filePath}")]
    public IActionResult StreamSourceAudio(string filePath)
    {
        return StreamProtectedFile("sources", filePath);
    }

    [HttpGet("working/{*filePath}")]
    public IActionResult StreamWorkingAudio(string filePath)
    {
        return StreamProtectedFile("working", filePath);
    }

    private IActionResult StreamProtectedFile(string folder, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains(".."))
        {
            return BadRequest(new { message = "مسار الملف غير صالح." });
        }

        var relativeKey = $"{folder}/{filePath}";
        var physicalPath = _storageService.GetPhysicalPath(relativeKey);

        // Security check: ensure path is strictly inside the designated folder
        var folderBasePath = _storageService.GetPhysicalPath(folder);
        var fullPhysicalPath = Path.GetFullPath(physicalPath);
        var fullFolderBase = Path.GetFullPath(folderBasePath);

        if (!fullPhysicalPath.StartsWith(fullFolderBase, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Potential path traversal attempt blocked: {Path}", filePath);
            return Forbid();
        }

        if (!System.IO.File.Exists(fullPhysicalPath))
        {
            return NotFound(new { message = "الملف الصوتي غير موجود." });
        }

        // Return with Range Processing enabled for scrubbing/seeking (HTTP 206 Partial Content)
        return PhysicalFile(fullPhysicalPath, "audio/mpeg", enableRangeProcessing: true);
    }
}
