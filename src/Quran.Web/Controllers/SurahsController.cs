using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurahsController : ControllerBase
{
    private readonly IQuranReaderService _quranReaderService;

    public SurahsController(IQuranReaderService quranReaderService)
    {
        _quranReaderService = quranReaderService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SurahDto>>> GetAll(CancellationToken cancellationToken)
    {
        var surahs = await _quranReaderService.GetAllSurahsAsync(cancellationToken);
        return Ok(surahs);
    }

    [HttpGet("{number:int}")]
    public async Task<ActionResult<SurahDto>> GetByNumber(int number, CancellationToken cancellationToken)
    {
        var surah = await _quranReaderService.GetSurahByNumberAsync(number, cancellationToken);
        if (surah == null)
            return NotFound(new { message = $"Sourate #{number} non trouvée." });

        return Ok(surah);
    }

    [HttpGet("{number:int}/ayahs")]
    public async Task<ActionResult<IReadOnlyList<AyahDto>>> GetAyahs(int number, CancellationToken cancellationToken)
    {
        var ayahs = await _quranReaderService.GetAyahsBySurahAsync(number, cancellationToken);
        return Ok(ayahs);
    }
}
