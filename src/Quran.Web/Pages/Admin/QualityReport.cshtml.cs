using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;

namespace Quran.Web.Pages.Admin;

[Authorize(Roles = "Admin,Reviewer")]
public class QualityReportModel : PageModel
{
    private readonly IQuranQualityAuditService _auditService;

    public QualityReportModel(IQuranQualityAuditService auditService)
    {
        _auditService = auditService;
    }

    public GlobalQualityReportDto Report { get; private set; } = null!;

    [BindProperty(SupportsGet = true)]
    public int? SurahFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AnomalyTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SeverityFilter { get; set; }

    public IReadOnlyList<QualityAnomalyDto> FilteredAnomalies { get; private set; } = Array.Empty<QualityAnomalyDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Report = await _auditService.GenerateGlobalQualityReportAsync(cancellationToken);

        var query = Report.Anomalies.AsEnumerable();

        if (SurahFilter.HasValue && SurahFilter.Value > 0)
        {
            query = query.Where(a => a.SurahNumber == SurahFilter.Value);
        }

        if (!string.IsNullOrEmpty(AnomalyTypeFilter) && Enum.TryParse<QualityAnomalyType>(AnomalyTypeFilter, out var typeVal))
        {
            query = query.Where(a => a.Type == typeVal);
        }

        if (!string.IsNullOrEmpty(SeverityFilter) && Enum.TryParse<QualityAnomalySeverity>(SeverityFilter, out var sevVal))
        {
            query = query.Where(a => a.Severity == sevVal);
        }

        FilteredAnomalies = query.ToList();
    }
}
