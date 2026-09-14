using Microsoft.AspNetCore.Mvc;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AyahsController : ControllerBase
{
    private readonly IQuranReaderService _quranReaderService;

    public AyahsController(IQuranReaderService quranReaderService)
    {
        _quranReaderService = quranReaderService;
    }

    [HttpGet("{surahNumber:int}/{ayahNumber:int}")]
    public async Task<ActionResult<AyahDto>> GetAyah(int surahNumber, int ayahNumber, CancellationToken cancellationToken)
    {
        var ayah = await _quranReaderService.GetAyahAsync(surahNumber, ayahNumber, cancellationToken);
        if (ayah == null)
            return NotFound(new { message = $"Ayah {surahNumber}:{ayahNumber} non trouvée." });

        return Ok(ayah);
    }
}
