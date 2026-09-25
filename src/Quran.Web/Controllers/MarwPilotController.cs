using Microsoft.AspNetCore.Mvc;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/mushaf/marw-pilot")]
public sealed class MarwPilotController(MarwPilotCorpus corpus) : ControllerBase
{
    [HttpGet("next")]
    public IActionResult Next([FromQuery] string reference)
    {
        // Image corpus order is authoritative; continued ayat on two pages appear only once.
        var seen = new HashSet<string>();
        var found = false;
        foreach (var pageNumber in corpus.PageNumbers)
        foreach (var region in corpus.GetPage(pageNumber)!.Regions)
        {
            if (!seen.Add(region.Reference)) continue;
            if (found) return Ok(new { reference = region.Reference, pageNumber });
            if (region.Reference == reference) found = true;
        }
        return found ? NoContent() : NotFound();
    }

    [HttpGet("pages/{pageNumber:int}")]
    public IActionResult Page(int pageNumber)
    {
        var page = corpus.GetPage(pageNumber);
        return page == null ? NotFound() : Ok(new
        {
            edition = MarwPilotCorpus.EditionCode,
            revision = MarwPilotCorpus.Revision,
            validationStatus = "PilotPendingReview",
            openingGeometryReview = MarwOpeningReview.Current.ReviewVersion,
            audioMappingStatus = "PendingReview",
            page,
            imageUrl = $"/api/mushaf/marw-pilot/{MarwPilotCorpus.Revision}/pages/{pageNumber}/image.png"
        });
    }

    [HttpGet("{revision}/pages/{pageNumber:int}/image.png")]
    public IActionResult Image(string revision, int pageNumber)
    {
        if (revision != MarwPilotCorpus.Revision || corpus.GetPage(pageNumber) is not { } page)
            return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return new FileContentResult(corpus.GetImage(pageNumber)!, "image/png")
        {
            EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{page.Sha256}\"")
        };
    }
}
