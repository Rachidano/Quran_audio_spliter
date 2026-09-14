using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;

namespace Quran.Web.Pages.Admin;

[Authorize(Policy = "RequireReviewer")]
public class CorpusMappingModel : PageModel
{
    private readonly ICorpusResolutionService _corpusService;

    public CorpusMappingModel(ICorpusResolutionService corpusService)
    {
        _corpusService = corpusService;
    }

    public IReadOnlyList<QuranCorpus> Corpora { get; set; } = new List<QuranCorpus>();
    public IReadOnlyList<ReferenceMapping> Mappings { get; set; } = new List<ReferenceMapping>();

    [BindProperty(SupportsGet = true)]
    public int SelectedSurah { get; set; } = 2;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await _corpusService.EnsureCorporaInitializedAsync();
        Corpora = await _corpusService.GetAllCorporaAsync();
        Mappings = await _corpusService.GetMappingsForSurahAsync(SelectedSurah);
    }

    public async Task<IActionResult> OnPostUpdateMappingAsync(
        long mappingId,
        string targetRef,
        string cardinality,
        string description,
        string status,
        string proofNotes)
    {
        var existing = (await _corpusService.GetMappingsForSurahAsync(SelectedSurah))
            .FirstOrDefault(m => m.Id == mappingId);

        if (existing == null)
        {
            ErrorMessage = "لم يتم العثور على المطابقة المحددة.";
            return RedirectToPage(new { SelectedSurah });
        }

        existing.TargetReference = targetRef;
        existing.Cardinality = cardinality;
        existing.DifferenceDescription = description;
        existing.ReviewStatus = status;
        existing.ProofNotes = proofNotes;
        existing.ReviewedByUserId = User.Identity?.Name ?? "Admin";
        existing.ReviewedAt = DateTime.UtcNow;

        await _corpusService.SaveMappingAsync(existing);
        SuccessMessage = $"تم تحديث مطابقة الآية {existing.SourceReference} بنجاح.";
        return RedirectToPage(new { SelectedSurah });
    }
}
