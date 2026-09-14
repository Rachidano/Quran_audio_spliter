using Microsoft.AspNetCore.Mvc;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/mushaf/warsh-pilot")]
public sealed class WarshPilotController(WarshPilotCorpus corpus) : ControllerBase
{
    [HttpGet("pages/{pageNumber:int}")]
    public IActionResult Page(int pageNumber)
    {
        var page = corpus.GetPage(pageNumber);
        return page == null ? NotFound() : Ok(new
        {
            edition = WarshPilotCorpus.EditionCode,
            revision = WarshPilotCorpus.Revision,
            validationStatus = "PilotPendingReview",
            audioMappingStatus = "PendingReview",
            page,
            imageUrl = $"/api/mushaf/warsh-pilot/{WarshPilotCorpus.Revision}/pages/{pageNumber}/image.svg"
        });
    }

    [HttpGet("{revision}/pages/{pageNumber:int}/image.svg")]
    public IActionResult Image(string revision, int pageNumber)
    {
        if (revision != WarshPilotCorpus.Revision || corpus.GetPage(pageNumber) is not { } page)
            return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return new FileContentResult(corpus.GetImage(pageNumber)!, "image/svg+xml")
        {
            EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{page.Sha256}\"")
        };
    }
}
