using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Quran.Import.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
public class ImportController : ControllerBase
{
    private readonly QuranSeedService _seedService;
    private readonly WarshAyahImportService _ayahImportService;
    private readonly AudioIngestionService _audioIngestionService;
    private readonly IWebHostEnvironment _environment;

    public ImportController(
        QuranSeedService seedService,
        WarshAyahImportService ayahImportService,
        AudioIngestionService audioIngestionService,
        IWebHostEnvironment environment)
    {
        _seedService = seedService;
        _ayahImportService = ayahImportService;
        _audioIngestionService = audioIngestionService;
        _environment = environment;
    }

    [HttpPost("seed-reference")]
    public async Task<IActionResult> SeedReferenceData(CancellationToken cancellationToken)
    {
        var basePath = GetReferenceBasePath();
        var surahsJsonPath = Path.Combine(basePath, "surahs.json");
        await _seedService.SeedReferenceDataAsync(surahsJsonPath, cancellationToken);

        return Ok(new { message = "Référentiel des 114 sourates et données de base initialisés avec succès." });
    }

    [HttpPost("import-fatiha")]
    public async Task<IActionResult> ImportFatiha(CancellationToken cancellationToken)
    {
        var basePath = GetReferenceBasePath();
        var fatihaJsonPath = Path.Combine(basePath, "warsh_fatiha.json");
        var count = await _ayahImportService.ImportAyahsFromJsonAsync(fatihaJsonPath, cancellationToken);

        return Ok(new { message = $"{count} versets de la Fatiha (Warsh) importés avec succès." });
    }

    [HttpPost("process-fatiha-audio")]
    public async Task<IActionResult> ProcessFatihaAudio(CancellationToken cancellationToken)
    {
        var refBasePath = GetReferenceBasePath();
        var audioBasePath = GetAudioBasePath();

        var sourcePath = Path.Combine(audioBasePath, "sources", "001", "original.mp3");
        if (!System.IO.File.Exists(sourcePath))
        {
            return BadRequest(new { message = $"Fichier source {sourcePath} non trouvé. Veuillez télécharger la sourate 1 d'abord." });
        }

        var segmentsJsonPath = Path.Combine(refBasePath, "audio_fatiha_segments.json");
        if (!System.IO.File.Exists(segmentsJsonPath))
        {
            return BadRequest(new { message = $"Fichier de définition des segments {segmentsJsonPath} non trouvé." });
        }

        var json = await System.IO.File.ReadAllTextAsync(segmentsJsonPath, cancellationToken);
        var rawSegments = JsonSerializer.Deserialize<List<SegmentInputModel>>(json);

        if (rawSegments == null || rawSegments.Count == 0)
        {
            return BadRequest(new { message = "Aucun segment défini dans le fichier JSON." });
        }

        var definitions = rawSegments.Select(s => new AyahSegmentDefinition(
            s.SurahNumber,
            s.AyahNumber,
            s.WajhCode,
            TimeSpan.FromSeconds(s.StartTimeSeconds),
            TimeSpan.FromSeconds(s.DurationSeconds)
        )).ToList();

        var publishedCount = await _audioIngestionService.ProcessAndPublishSegmentsAsync(sourcePath, definitions, cancellationToken);

        return Ok(new { message = $"{publishedCount} segments audio d'Al-Fatiha découpés, analysés et publiés avec succès." });
    }

    [HttpPost("import-baqarah-1-20")]
    public async Task<IActionResult> ImportBaqarah(CancellationToken cancellationToken)
    {
        var basePath = GetReferenceBasePath();
        var baqarahJsonPath = Path.Combine(basePath, "warsh_baqarah_1_20.json");
        var count = await _ayahImportService.ImportAyahsFromJsonAsync(baqarahJsonPath, cancellationToken);

        return Ok(new { message = $"{count} versets de la sourate Al-Baqarah (Warsh) importés avec succès." });
    }

    [HttpPost("process-baqarah-audio")]
    public async Task<IActionResult> ProcessBaqarahAudio(CancellationToken cancellationToken)
    {
        var refBasePath = GetReferenceBasePath();
        var audioBasePath = GetAudioBasePath();

        var sourcePath = Path.Combine(audioBasePath, "sources", "002", "original.mp3");
        if (!System.IO.File.Exists(sourcePath))
        {
            return BadRequest(new { message = $"Fichier source {sourcePath} non trouvé. Veuillez télécharger la sourate 2 d'abord." });
        }

        var segmentsJsonPath = Path.Combine(refBasePath, "audio_baqarah_1_20_segments.json");
        if (!System.IO.File.Exists(segmentsJsonPath))
        {
            return BadRequest(new { message = $"Fichier de définition des segments {segmentsJsonPath} non trouvé." });
        }

        var json = await System.IO.File.ReadAllTextAsync(segmentsJsonPath, cancellationToken);
        var rawSegments = JsonSerializer.Deserialize<List<SegmentInputModel>>(json);

        if (rawSegments == null || rawSegments.Count == 0)
        {
            return BadRequest(new { message = "Aucun segment défini dans le fichier JSON." });
        }

        var definitions = rawSegments.Select(s => new AyahSegmentDefinition(
            s.SurahNumber,
            s.AyahNumber,
            s.WajhCode,
            TimeSpan.FromSeconds(s.StartTimeSeconds),
            TimeSpan.FromSeconds(s.DurationSeconds)
        )).ToList();

        var publishedCount = await _audioIngestionService.ProcessAndPublishSegmentsAsync(sourcePath, definitions, cancellationToken);

        return Ok(new { message = $"{publishedCount} segments audio d'Al-Baqarah (1-20) découpés, analysés et publiés avec succès." });
    }

    private string GetReferenceBasePath()
    {
        var candidate1 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "reference");
        if (Directory.Exists(candidate1)) return Path.GetFullPath(candidate1);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "reference"));
    }

    private string GetAudioBasePath()
    {
        var candidate1 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "audio");
        if (Directory.Exists(candidate1)) return Path.GetFullPath(candidate1);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "audio"));
    }

    private record SegmentInputModel(
        int SurahNumber,
        int AyahNumber,
        string WajhCode,
        double StartTimeSeconds,
        double DurationSeconds
    );
}
