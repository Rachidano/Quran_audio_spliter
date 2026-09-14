using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Web.Pages.Admin;

[Authorize(Roles = "Reviewer,Admin")]
public class ReviewQueueModel : PageModel
{
    private readonly IAudioEditorService _editorService;
    private readonly QuranDbContext _db;
    private readonly ILogger<ReviewQueueModel> _logger;

    public ReviewQueueModel(
        IAudioEditorService editorService,
        QuranDbContext db,
        ILogger<ReviewQueueModel> logger)
    {
        _editorService = editorService;
        _db = db;
        _logger = logger;
    }

    public List<Surah> Surahs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SurahNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public AudioSegmentStatus? Status { get; set; }

    public IReadOnlyList<AudioSegmentDto> Segments { get; set; } = Array.Empty<AudioSegmentDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Surahs = await _db.Surahs.OrderBy(s => s.Number).AsNoTracking().ToListAsync(cancellationToken);
        Segments = await _editorService.GetReviewQueueAsync(SurahNumber, Status, cancellationToken);
    }
}
