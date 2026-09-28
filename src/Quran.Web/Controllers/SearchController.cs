using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IQuranReaderService _quranReaderService;

    public SearchController(IQuranReaderService quranReaderService)
    {
        _quranReaderService = quranReaderService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search([FromQuery] string q, [FromQuery] int limit = int.MaxValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<SearchResultDto>());

        var results = await _quranReaderService.SearchAsync(q, limit, cancellationToken);
        return Ok(results);
    }
}
