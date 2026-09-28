using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class AwjuhService : IAwjuhService
{
    private readonly QuranDbContext _db;

    public AwjuhService(QuranDbContext db)
    {
        _db = db;
    }

    public async Task<List<WajhDto>> SearchAsync(AwjuhSearchFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Awjuh
            .AsNoTracking()
            .Include(w => w.TransmissionRoute)
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Include(w => w.Citations).ThenInclude(c => c.ReferenceSource)
            .Include(w => w.Revisions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.RouteCode))
        {
            query = query.Where(w => w.RouteCode == filter.RouteCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.RuleGroup))
        {
            query = query.Where(w => w.RuleGroup == filter.RuleGroup);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(w => w.ReviewStatus == filter.Status);
        }

        if (filter.SurahNumber.HasValue)
        {
            query = query.Where(w => !w.Scopes.Any() || w.Scopes.Any(s => !s.SourceSurah.HasValue || s.SourceSurah == filter.SurahNumber.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.Trim();
            query = query.Where(w => w.NameArabic.Contains(q) || w.Code.Contains(q) || (w.Description != null && w.Description.Contains(q)));
        }

        var list = await query
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        var wajhIds = list.Select(w => w.Id).ToList();
        var usageCounts = await GetUsageCountsMapAsync(wajhIds, cancellationToken);

        if (filter.HasUsages.HasValue)
        {
            list = list.Where(w => filter.HasUsages.Value ? (usageCounts.GetValueOrDefault(w.Id, 0) > 0) : (usageCounts.GetValueOrDefault(w.Id, 0) == 0)).ToList();
        }

        return list.Select(w => MapToDto(w, usageCounts.GetValueOrDefault(w.Id, 0))).ToList();
    }

    public async Task<WajhDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var w = await _db.Awjuh
            .AsNoTracking()
            .Include(x => x.TransmissionRoute)
            .Include(x => x.Components)
            .Include(x => x.Scopes)
            .Include(x => x.Citations).ThenInclude(c => c.ReferenceSource)
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (w == null) return null;
        var count = await GetSingleUsageCountAsync(id, cancellationToken);
        return MapToDto(w, count);
    }

    public async Task<WajhDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var w = await _db.Awjuh
            .AsNoTracking()
            .Include(x => x.TransmissionRoute)
            .Include(x => x.Components)
            .Include(x => x.Scopes)
            .Include(x => x.Citations).ThenInclude(c => c.ReferenceSource)
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        if (w == null) return null;
        var count = await GetSingleUsageCountAsync(w.Id, cancellationToken);
        return MapToDto(w, count);
    }

    public async Task<List<WajhDto>> GetAvailableAwjuhForReaderAsync(int? surahNumber, string? reference, CancellationToken cancellationToken = default)
    {
        // Reader policy: ONLY Approved wajh definitions that have at least one Published audio occurrence
        var publishedWajhIdsQuery = _db.AudioSegments
            .AsNoTracking()
            .Where(s => s.Status == AudioSegmentStatus.Approved && s.PublishedAudioAssetId != null && s.WajhId != null);

        if (!string.IsNullOrWhiteSpace(reference))
        {
            publishedWajhIdsQuery = publishedWajhIdsQuery.Where(s => s.Reference == reference);
        }

        var publishedWajhIds = await publishedWajhIdsQuery
            .Select(s => s.WajhId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Also check published recitations
        var recPublishedIdsQuery = _db.Recitations
            .AsNoTracking()
            .Where(r => r.IsPublished && r.WajhId != null);

        if (surahNumber.HasValue)
        {
            recPublishedIdsQuery = recPublishedIdsQuery.Where(r => r.Ayah != null && r.Ayah.SurahId == surahNumber.Value);
        }

        var recPublishedIds = await recPublishedIdsQuery
            .Select(r => r.WajhId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var validIds = publishedWajhIds.Union(recPublishedIds).Distinct().ToList();

        if (validIds.Count == 0) return new List<WajhDto>();

        var awjuh = await _db.Awjuh
            .AsNoTracking()
            .Include(w => w.TransmissionRoute)
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Where(w => validIds.Contains(w.Id) && w.ReviewStatus == "Approved")
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        return awjuh.Select(w => MapToDto(w, 1)).ToList();
    }

    public async Task<List<WajhDto>> GetCandidateAwjuhForEditorAsync(int? surahNumber, string? reference, CancellationToken cancellationToken = default)
    {
        // Editor candidates: active (Approved or PendingReview) definitions
        var query = _db.Awjuh
            .AsNoTracking()
            .Include(w => w.TransmissionRoute)
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Where(w => w.ReviewStatus == "Approved" || w.ReviewStatus == "PendingReview")
            .AsQueryable();

        if (surahNumber.HasValue)
        {
            query = query.Where(w => !w.Scopes.Any() || w.Scopes.Any(s => !s.SourceSurah.HasValue || s.SourceSurah == surahNumber.Value));
        }

        var awjuh = await query
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        var counts = await GetUsageCountsMapAsync(awjuh.Select(w => w.Id).ToList(), cancellationToken);
        return awjuh.Select(w => MapToDto(w, counts.GetValueOrDefault(w.Id, 0))).ToList();
    }

    public async Task<WajhDto> CreateDraftAsync(CreateWajhDto dto, string? userId, CancellationToken cancellationToken = default)
    {
        ValidateCode(dto.Code);
        ValidateRequiredFields(dto.NameArabic, dto.RouteCode, dto.Description, dto.SourceCitation);

        if (await _db.Awjuh.AnyAsync(w => w.Code == dto.Code, cancellationToken))
        {
            throw new InvalidOperationException($"الرمز '{dto.Code}' مستخدم مسبقاً.");
        }

        var route = await _db.TransmissionRoutes.FirstOrDefaultAsync(r => r.Code == dto.RouteCode, cancellationToken);

        var wajh = new Wajh
        {
            Code = dto.Code.Trim().ToLowerInvariant(),
            NameArabic = dto.NameArabic.Trim(),
            RouteCode = dto.RouteCode.Trim(),
            TransmissionRouteId = route?.Id,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "Tahrirat" : dto.Category.Trim(),
            RuleGroup = dto.RuleGroup?.Trim(),
            Description = dto.Description.Trim(),
            ExcludedScope = dto.ExcludedScope?.Trim(),
            SourceCitation = dto.SourceCitation.Trim(),
            ReviewStatus = "Draft",
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (dto.Components != null)
        {
            foreach (var c in dto.Components)
            {
                wajh.Components.Add(new WajhComponent
                {
                    ComponentKey = c.ComponentKey.Trim(),
                    ValueArabic = c.ValueArabic.Trim(),
                    ValueNumeric = c.ValueNumeric
                });
            }
        }

        if (dto.Scopes != null)
        {
            foreach (var s in dto.Scopes)
            {
                wajh.Scopes.Add(new WajhApplicationScope
                {
                    SourceSurah = s.SourceSurah,
                    SourceAyahStart = s.SourceAyahStart,
                    SourceAyahEnd = s.SourceAyahEnd,
                    MarwMappingStatus = s.MarwMappingStatus,
                    AutomaticAssignment = s.AutomaticAssignment,
                    ContextDescription = s.ContextDescription
                });
            }
        }

        if (dto.ReferenceSourceIds != null)
        {
            foreach (var srcId in dto.ReferenceSourceIds)
            {
                wajh.Citations.Add(new EvidenceCitation
                {
                    ReferenceSourceId = srcId,
                    Locator = dto.SourceCitation
                });
            }
        }

        // Create Initial Revision
        wajh.Revisions.Add(new WajhRevision
        {
            RevisionNumber = 1,
            Code = wajh.Code,
            NameArabic = wajh.NameArabic,
            RouteCode = wajh.RouteCode,
            ReviewStatus = wajh.ReviewStatus,
            DefinitionSnapshotJson = SerializeSnapshot(wajh),
            ChangedByUserId = userId ?? "admin",
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = "إنشاء المسودة الأولى للوجه",
            AffectsSemanticRecitation = true
        });

        _db.Awjuh.Add(wajh);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? "admin",
            Action = "CREATE_WAJH_DRAFT",
            EntityType = "Wajh",
            EntityId = wajh.Code,
            NewValuesJson = $"إنشاء مسودة وجه جديد: {wajh.NameArabic} ({wajh.Code})"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(wajh, 0);
    }

    public async Task<WajhDto> UpdateAsync(int id, UpdateWajhDto dto, string? userId, CancellationToken cancellationToken = default)
    {
        ValidateRequiredFields(dto.NameArabic, dto.RouteCode, dto.Description, dto.SourceCitation);

        var wajh = await _db.Awjuh
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Include(w => w.Citations)
            .Include(w => w.Revisions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (wajh == null)
        {
            throw new KeyNotFoundException($"الوجه برقم {id} غير موجود.");
        }

        if (dto.Version != wajh.Version)
        {
            throw new DbUpdateConcurrencyException("تغير الوجه في جلسة أخرى. أعد تحميل الصفحة للتحديث.");
        }

        var isApproved = wajh.ReviewStatus == "Approved";

        wajh.NameArabic = dto.NameArabic.Trim();
        wajh.RouteCode = dto.RouteCode.Trim();
        var route = await _db.TransmissionRoutes.FirstOrDefaultAsync(r => r.Code == dto.RouteCode, cancellationToken);
        wajh.TransmissionRouteId = route?.Id;
        wajh.Category = string.IsNullOrWhiteSpace(dto.Category) ? "Tahrirat" : dto.Category.Trim();
        wajh.RuleGroup = dto.RuleGroup?.Trim();
        wajh.Description = dto.Description.Trim();
        wajh.ExcludedScope = dto.ExcludedScope?.Trim();
        wajh.SourceCitation = dto.SourceCitation.Trim();
        wajh.UpdatedAt = DateTimeOffset.UtcNow;
        wajh.Version++;

        // Update components
        _db.WajhComponents.RemoveRange(wajh.Components);
        if (dto.Components != null)
        {
            foreach (var c in dto.Components)
            {
                wajh.Components.Add(new WajhComponent
                {
                    WajhId = wajh.Id,
                    ComponentKey = c.ComponentKey.Trim(),
                    ValueArabic = c.ValueArabic.Trim(),
                    ValueNumeric = c.ValueNumeric
                });
            }
        }

        // Update scopes
        _db.WajhApplicationScopes.RemoveRange(wajh.Scopes);
        if (dto.Scopes != null)
        {
            foreach (var s in dto.Scopes)
            {
                wajh.Scopes.Add(new WajhApplicationScope
                {
                    WajhId = wajh.Id,
                    SourceSurah = s.SourceSurah,
                    SourceAyahStart = s.SourceAyahStart,
                    SourceAyahEnd = s.SourceAyahEnd,
                    MarwMappingStatus = s.MarwMappingStatus,
                    AutomaticAssignment = s.AutomaticAssignment,
                    ContextDescription = s.ContextDescription
                });
            }
        }

        // Update citations
        _db.EvidenceCitations.RemoveRange(wajh.Citations);
        if (dto.ReferenceSourceIds != null)
        {
            foreach (var srcId in dto.ReferenceSourceIds)
            {
                wajh.Citations.Add(new EvidenceCitation
                {
                    WajhId = wajh.Id,
                    ReferenceSourceId = srcId,
                    Locator = dto.SourceCitation
                });
            }
        }

        // If it was already approved, this is a semantic update: create a new versioned revision
        if (isApproved)
        {
            var nextRev = wajh.Revisions.Count > 0 ? wajh.Revisions.Max(r => r.RevisionNumber) + 1 : 2;
            wajh.Revisions.Add(new WajhRevision
            {
                WajhId = wajh.Id,
                RevisionNumber = nextRev,
                Code = wajh.Code,
                NameArabic = wajh.NameArabic,
                RouteCode = wajh.RouteCode,
                ReviewStatus = wajh.ReviewStatus,
                DefinitionSnapshotJson = SerializeSnapshot(wajh),
                ChangedByUserId = userId ?? "admin",
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeReason = string.IsNullOrWhiteSpace(dto.ChangeReason) ? "تعديل في تعريف الوجه المعتمد" : dto.ChangeReason.Trim(),
                AffectsSemanticRecitation = true
            });

            // Dependent unreviewed occurrences must be marked for re-review
            var segments = await _db.AudioSegments.Where(s => s.WajhId == id).ToListAsync(cancellationToken);
            foreach (var s in segments)
            {
                s.Status = AudioSegmentStatus.Draft;
                s.ValidatedAt = null;
                s.ValidatedByUserId = null;
                s.PublishedAudioAssetId = null;
                s.Version++;
            }

            var recitations = await _db.Recitations.Where(r => r.WajhId == id).ToListAsync(cancellationToken);
            foreach (var r in recitations)
            {
                r.Status = RecitationStatus.Imported;
                r.IsPublished = false;
            }

            var passages = await _db.MushafPassages.Where(p => p.WajhId == id).ToListAsync(cancellationToken);
            foreach (var p in passages)
            {
                p.Status = "Draft";
                p.Version++;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? "admin",
            Action = isApproved ? "UPDATE_APPROVED_WAJH" : "UPDATE_WAJH_DRAFT",
            EntityType = "Wajh",
            EntityId = wajh.Code,
            NewValuesJson = $"تعديل الوجه {wajh.Code} (نسخة {wajh.Version}). سبب التعديل: {dto.ChangeReason ?? "تحديث عادي"}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        var usages = await GetSingleUsageCountAsync(wajh.Id, cancellationToken);
        return MapToDto(wajh, usages);
    }

    public async Task<WajhDto> ReviewAsync(int id, ReviewWajhDto dto, string? userId, CancellationToken cancellationToken = default)
    {
        var wajh = await _db.Awjuh
            .Include(w => w.TransmissionRoute)
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Include(w => w.Citations).ThenInclude(c => c.ReferenceSource)
            .Include(w => w.Revisions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (wajh == null)
        {
            throw new KeyNotFoundException($"الوجه برقم {id} غير موجود.");
        }

        switch (dto.Action)
        {
            case "Submit":
                wajh.ReviewStatus = "PendingReview";
                wajh.RejectionReason = null;
                break;

            case "Approve":
                wajh.ReviewStatus = "Approved";
                wajh.ReviewerUserId = userId ?? "reviewer";
                wajh.ReviewedAt = DateTimeOffset.UtcNow;
                wajh.RejectionReason = null;
                break;

            case "Reject":
                if (string.IsNullOrWhiteSpace(dto.Reason))
                {
                    throw new ArgumentException("سبب الرفض إلزامي عند رفض الوجه.");
                }
                wajh.ReviewStatus = "Rejected";
                wajh.ReviewerUserId = userId ?? "reviewer";
                wajh.ReviewedAt = DateTimeOffset.UtcNow;
                wajh.RejectionReason = dto.Reason.Trim();
                break;

            case "Archive":
                if (string.IsNullOrWhiteSpace(dto.Reason))
                {
                    throw new ArgumentException("سبب الأرشفة إلزامي عند سحب الوجه.");
                }
                wajh.ReviewStatus = "Archived";
                wajh.RejectionReason = dto.Reason.Trim();
                break;

            default:
                throw new ArgumentException($"إجراء المراجعة غير صالح: {dto.Action}");
        }

        wajh.UpdatedAt = DateTimeOffset.UtcNow;
        wajh.Version++;

        // Add a revision documenting the review decision
        var nextRev = wajh.Revisions.Count > 0 ? wajh.Revisions.Max(r => r.RevisionNumber) + 1 : 1;
        wajh.Revisions.Add(new WajhRevision
        {
            WajhId = wajh.Id,
            RevisionNumber = nextRev,
            Code = wajh.Code,
            NameArabic = wajh.NameArabic,
            RouteCode = wajh.RouteCode,
            ReviewStatus = wajh.ReviewStatus,
            DefinitionSnapshotJson = SerializeSnapshot(wajh),
            ChangedByUserId = userId ?? "reviewer",
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = $"قرار مراجعة: {dto.Action} - {dto.Reason ?? "دون ملاحظات إضافية"}",
            AffectsSemanticRecitation = dto.Action == "Archive" || dto.Action == "Reject"
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? "reviewer",
            Action = $"REVIEW_WAJH_{dto.Action.ToUpperInvariant()}",
            EntityType = "Wajh",
            EntityId = wajh.Code,
            NewValuesJson = $"تنفيذ إجراء {dto.Action} على الوجه {wajh.Code}. السبب: {dto.Reason ?? "لا يوجد"}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        var count = await GetSingleUsageCountAsync(wajh.Id, cancellationToken);
        return MapToDto(wajh, count);
    }

    public async Task<WajhDto> DuplicateAsync(int id, string newCode, string? userId, CancellationToken cancellationToken = default)
    {
        ValidateCode(newCode);

        if (await _db.Awjuh.AnyAsync(w => w.Code == newCode, cancellationToken))
        {
            throw new InvalidOperationException($"الرمز الجديد '{newCode}' مستخدم مسبقاً.");
        }

        var source = await _db.Awjuh
            .AsNoTracking()
            .Include(w => w.Components)
            .Include(w => w.Scopes)
            .Include(w => w.Citations)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (source == null)
        {
            throw new KeyNotFoundException($"الوجه المصدر برقم {id} غير موجود.");
        }

        var duplicated = new Wajh
        {
            Code = newCode.Trim().ToLowerInvariant(),
            NameArabic = $"{source.NameArabic} (نسخة)",
            RouteCode = source.RouteCode,
            TransmissionRouteId = source.TransmissionRouteId,
            Category = source.Category,
            RuleGroup = source.RuleGroup,
            Description = source.Description,
            ExcludedScope = source.ExcludedScope,
            SourceCitation = source.SourceCitation,
            ReviewStatus = "Draft", // Always Draft, never copy approval
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var c in source.Components)
        {
            duplicated.Components.Add(new WajhComponent
            {
                ComponentKey = c.ComponentKey,
                ValueArabic = c.ValueArabic,
                ValueNumeric = c.ValueNumeric
            });
        }

        foreach (var s in source.Scopes)
        {
            duplicated.Scopes.Add(new WajhApplicationScope
            {
                SourceSurah = s.SourceSurah,
                SourceAyahStart = s.SourceAyahStart,
                SourceAyahEnd = s.SourceAyahEnd,
                MarwMappingStatus = s.MarwMappingStatus,
                AutomaticAssignment = false,
                ContextDescription = s.ContextDescription
            });
        }

        foreach (var cit in source.Citations)
        {
            duplicated.Citations.Add(new EvidenceCitation
            {
                ReferenceSourceId = cit.ReferenceSourceId,
                Locator = cit.Locator,
                QuoteOrNote = cit.QuoteOrNote
            });
        }

        duplicated.Revisions.Add(new WajhRevision
        {
            RevisionNumber = 1,
            Code = duplicated.Code,
            NameArabic = duplicated.NameArabic,
            RouteCode = duplicated.RouteCode,
            ReviewStatus = "Draft",
            DefinitionSnapshotJson = SerializeSnapshot(duplicated),
            ChangedByUserId = userId ?? "admin",
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = $"منسوخ من الوجه {source.Code} (معرف {source.Id})",
            AffectsSemanticRecitation = true
        });

        _db.Awjuh.Add(duplicated);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? "admin",
            Action = "DUPLICATE_WAJH",
            EntityType = "Wajh",
            EntityId = duplicated.Code,
            NewValuesJson = $"نسخ الوجه {source.Code} إلى رمز جديد {duplicated.Code}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(duplicated, 0);
    }

    public async Task DeleteDraftAsync(int id, int version, CancellationToken cancellationToken = default)
    {
        var wajh = await _db.Awjuh.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (wajh == null)
        {
            throw new KeyNotFoundException($"الوجه برقم {id} غير موجود.");
        }

        if (wajh.Version != version)
        {
            throw new DbUpdateConcurrencyException("أعد تحميل المرجع قبل الحذف.");
        }

        if (wajh.ReviewStatus != "Draft")
        {
            throw new InvalidOperationException("لا يمكن حذف وجه إلا إذا كان في حالة مسودة (Draft). للأوجه المعتمدة، استخدم الأرشفة.");
        }

        var segmentCount = await _db.AudioSegments.CountAsync(s => s.WajhId == id, cancellationToken);
        var recCount = await _db.Recitations.CountAsync(r => r.WajhId == id, cancellationToken);
        var passageCount = await _db.MushafPassages.CountAsync(p => p.WajhId == id, cancellationToken);

        if (segmentCount > 0 || recCount > 0 || passageCount > 0)
        {
            throw new InvalidOperationException($"الوجه مرتبط بـ ({segmentCount} مقطع صوتي، {recCount} تلاوة، {passageCount} مقطع مصحف). لا يمكن حذفه؛ اختر الأرشفة بدلاً من الحذف.");
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = "admin",
            Action = "DELETE_WAJH_DRAFT",
            EntityType = "Wajh",
            EntityId = wajh.Code,
            NewValuesJson = $"حذف مسودة الوجه {wajh.Code} ({wajh.NameArabic})"
        });

        _db.Awjuh.Remove(wajh);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WajhUsageReportDto> GetUsageReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var wajh = await _db.Awjuh.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (wajh == null)
        {
            throw new KeyNotFoundException($"الوجه برقم {id} غير موجود.");
        }

        var segments = await _db.AudioSegments
            .AsNoTracking()
            .Where(s => s.WajhId == id)
            .OrderBy(s => s.StartMs)
            .Select(s => new AudioSegmentUsageDto(s.Id, s.Reference, s.StartMs, s.EndMs, s.Status.ToString(), s.Edition, s.SourceAudioId))
            .ToListAsync(cancellationToken);

        var recitations = await _db.Recitations
            .AsNoTracking()
            .Where(r => r.WajhId == id)
            .Select(r => new RecitationUsageDto(r.Id, r.Ayah != null ? r.Ayah.SurahId : 0, r.Ayah != null ? r.Ayah.AyahNumber : 0, r.Status.ToString(), r.IsPublished))
            .ToListAsync(cancellationToken);

        var passageCount = await _db.MushafPassages.CountAsync(p => p.WajhId == id, cancellationToken);

        return new WajhUsageReportDto(
            WajhId: wajh.Id,
            Code: wajh.Code,
            NameArabic: wajh.NameArabic,
            ReviewStatus: wajh.ReviewStatus,
            TotalUsages: segments.Count + recitations.Count + passageCount,
            AudioSegmentsCount: segments.Count,
            RecitationsCount: recitations.Count,
            MushafPassagesCount: passageCount,
            AudioSegments: segments,
            Recitations: recitations
        );
    }

    public async Task<List<TransmissionRouteDto>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await _db.TransmissionRoutes
            .AsNoTracking()
            .Include(r => r.Awjuh)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return routes.Select(r => new TransmissionRouteDto(
            Id: r.Id,
            Code: r.Code,
            NameArabic: r.NameArabic,
            Riwaya: r.Riwaya,
            Tariq: r.Tariq,
            SubTariq: r.SubTariq,
            CoverageStatus: r.CoverageStatus,
            AwjuhCount: r.Awjuh.Count
        )).ToList();
    }

    public async Task<List<ReferenceSourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _db.ReferenceSources
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return sources.Select(s => new ReferenceSourceDto(
            Id: s.Id,
            SourceCode: s.SourceCode,
            Title: s.Title,
            Author: s.Author,
            Locator: s.Locator,
            Url: s.Url,
            Sha256: s.Sha256,
            Kind: s.Kind
        )).ToList();
    }

    public async Task<List<CombinationConstraintDto>> GetConstraintsAsync(string? routeCode = null, CancellationToken cancellationToken = default)
    {
        var query = _db.CombinationConstraints.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(routeCode))
        {
            query = query.Where(c => c.RouteCode == routeCode);
        }

        var constraints = await query.OrderBy(c => c.Id).ToListAsync(cancellationToken);

        return constraints.Select(c => new CombinationConstraintDto(
            Id: c.Id,
            Code: c.Code,
            RouteCode: c.RouteCode,
            NameArabic: c.NameArabic,
            ContextArabic: c.ContextArabic,
            ConditionJson: c.ConditionJson,
            EffectJson: c.EffectJson,
            ExplanationArabic: c.ExplanationArabic,
            ReviewStatus: c.ReviewStatus,
            AutomaticEnforcement: c.AutomaticEnforcement,
            MarwMappingStatus: c.MarwMappingStatus
        )).ToList();
    }

    public async Task<List<DocumentaryCoverageEntryDto>> GetCoverageMatrixAsync(string? routeCode = null, CancellationToken cancellationToken = default)
    {
        var query = _db.DocumentaryCoverageEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(routeCode))
        {
            query = query.Where(e => e.TransmissionRouteCode == routeCode);
        }

        var entries = await query.OrderBy(e => e.Id).ToListAsync(cancellationToken);
        var routes = await _db.TransmissionRoutes.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.NameArabic, cancellationToken);

        return entries.Select(e => new DocumentaryCoverageEntryDto(
            Id: e.Id,
            TransmissionRouteCode: e.TransmissionRouteCode,
            RouteName: routes.GetValueOrDefault(e.TransmissionRouteCode, e.TransmissionRouteCode),
            SourceWorkTitle: e.SourceWorkTitle,
            ChapterOrSection: e.ChapterOrSection,
            SurahNumber: e.SurahNumber,
            ScopeContext: e.ScopeContext,
            AnalysisStatus: e.AnalysisStatus,
            EntriesCount: e.EntriesCount,
            ConstraintsCount: e.ConstraintsCount,
            Notes: e.Notes,
            LastReviewedAt: e.LastReviewedAt
        )).ToList();
    }

    public async Task<DocumentaryCoverageEntryDto> UpdateCoverageEntryAsync(UpdateCoverageEntryDto dto, string? userId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.DocumentaryCoverageEntries.FirstOrDefaultAsync(e => e.Id == dto.Id, cancellationToken);
        if (entry == null)
        {
            throw new KeyNotFoundException($"بند التغطية برقم {dto.Id} غير موجود.");
        }

        entry.AnalysisStatus = dto.AnalysisStatus;
        if (dto.Notes != null) entry.Notes = dto.Notes.Trim();
        entry.LastReviewedAt = DateTimeOffset.UtcNow;
        entry.ReviewedByUserId = userId ?? "reviewer";

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? "reviewer",
            Action = "UPDATE_COVERAGE_ENTRY",
            EntityType = "DocumentaryCoverageEntry",
            EntityId = entry.Id.ToString(),
            NewValuesJson = $"تحديث حالة تغطية {entry.ChapterOrSection} ({entry.TransmissionRouteCode}): {entry.AnalysisStatus}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        var route = await _db.TransmissionRoutes.FirstOrDefaultAsync(r => r.Code == entry.TransmissionRouteCode, cancellationToken);

        return new DocumentaryCoverageEntryDto(
            Id: entry.Id,
            TransmissionRouteCode: entry.TransmissionRouteCode,
            RouteName: route?.NameArabic ?? entry.TransmissionRouteCode,
            SourceWorkTitle: entry.SourceWorkTitle,
            ChapterOrSection: entry.ChapterOrSection,
            SurahNumber: entry.SurahNumber,
            ScopeContext: entry.ScopeContext,
            AnalysisStatus: entry.AnalysisStatus,
            EntriesCount: entry.EntriesCount,
            ConstraintsCount: entry.ConstraintsCount,
            Notes: entry.Notes,
            LastReviewedAt: entry.LastReviewedAt
        );
    }

    public async Task<int> EnsureSeedDataAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        // Safe bootstrap: never recreate if already seeded, and NEVER resurrect deleted legacy awjuh!
        var alreadySeeded = await _db.AuditLogs.AnyAsync(a => a.Action == "CATALOG_SEEDED_V1", cancellationToken);
        if (alreadySeeded && !force)
        {
            return 0;
        }

        var countAdded = 0;
        var root = AwjuhCatalog.Read();

        // 1. Seed Transmission Routes
        if (!await _db.TransmissionRoutes.AnyAsync(cancellationToken))
        {
            foreach (var r in root.GetProperty("routes").EnumerateArray())
            {
                var code = r.GetProperty("code").GetString()!;
                var name = r.GetProperty("name").GetString()!;
                var coverage = r.TryGetProperty("coverage", out var cov) ? cov.GetString()! : "Partial";

                var tariq = code.StartsWith("azraq") ? "الأزرق" : "الأصبهاني";
                var subTariq = code.EndsWith("shatibiyya") ? "الشاطبية" : "طيبة النشر";

                _db.TransmissionRoutes.Add(new TransmissionRoute
                {
                    Code = code,
                    NameArabic = name,
                    Riwaya = "ورش عن نافع",
                    Tariq = tariq,
                    SubTariq = subTariq,
                    CoverageStatus = coverage
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 2. Seed Reference Sources
        if (!await _db.ReferenceSources.AnyAsync(cancellationToken))
        {
            foreach (var s in root.GetProperty("sources").EnumerateArray())
            {
                var id = s.GetProperty("id").GetString()!;
                var title = s.GetProperty("title").GetString()!;
                var locator = s.TryGetProperty("locator", out var loc) ? loc.GetString() : null;
                var url = s.TryGetProperty("url", out var u) ? u.GetString() : null;
                var sha = s.TryGetProperty("sha256", out var sh) ? sh.GetString() : null;
                var kind = s.TryGetProperty("kind", out var k) ? k.GetString()! : "SpecialistStudy";

                _db.ReferenceSources.Add(new ReferenceSource
                {
                    SourceCode = id,
                    Title = title,
                    Locator = locator,
                    Url = url,
                    Sha256 = sha,
                    Kind = kind
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 3. Seed Reading Rules
        if (!await _db.ReadingRules.AnyAsync(cancellationToken))
        {
            var ruleBadal = new ReadingRule
            {
                Code = "badal-lin",
                NameArabic = "اجتماع مد البدل واللين المهموز",
                Category = "Tahrirat",
                Description = "أوجه تحرير اجتماع مد البدل واللين المهموز في رواية ورش"
            };
            ruleBadal.Realizations.Add(new ReadingRealization { Code = "badal-2", ValueArabic = "قصر البدل (حركتان)", ValueNumeric = 2 });
            ruleBadal.Realizations.Add(new ReadingRealization { Code = "badal-4", ValueArabic = "توسط البدل (4 حركات)", ValueNumeric = 4 });
            ruleBadal.Realizations.Add(new ReadingRealization { Code = "badal-6", ValueArabic = "إشباع البدل (6 حركات)", ValueNumeric = 6 });
            ruleBadal.Realizations.Add(new ReadingRealization { Code = "lin-4", ValueArabic = "توسط اللين المهموز (4 حركات)", ValueNumeric = 4 });
            ruleBadal.Realizations.Add(new ReadingRealization { Code = "lin-6", ValueArabic = "إشباع اللين المهموز (6 حركات)", ValueNumeric = 6 });

            var ruleYasin = new ReadingRule
            {
                Code = "yasin-opening",
                NameArabic = "فواتح السور — أول يس",
                Category = "Tahrirat",
                Description = "فروع تنقيح كتاب التحرير عند وصل أول يس"
            };
            ruleYasin.Realizations.Add(new ReadingRealization { Code = "yasin-fath", ValueArabic = "فتح يس" });
            ruleYasin.Realizations.Add(new ReadingRealization { Code = "yasin-taqlil", ValueArabic = "تقليل يس" });
            ruleYasin.Realizations.Add(new ReadingRealization { Code = "yasin-idgham", ValueArabic = "إدغام نون يس في الواو" });
            ruleYasin.Realizations.Add(new ReadingRealization { Code = "yasin-izhar", ValueArabic = "إظهار نون يس عند الواو" });

            _db.ReadingRules.AddRange(ruleBadal, ruleYasin);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 4. Seed Awjuh Definitions (Only if not existing and NOT legacy)
        var routesMap = await _db.TransmissionRoutes.ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);
        var sourcesMap = await _db.ReferenceSources.ToDictionaryAsync(s => s.SourceCode, s => s.Id, cancellationToken);
        var rulesMap = await _db.ReadingRules.ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);

        foreach (var entry in root.GetProperty("entries").EnumerateArray())
        {
            var code = entry.GetProperty("code").GetString()!;

            // Strict rule: NEVER recreate deleted legacy awjuh!
            if (AwjuhCatalog.LegacyCodes.Contains(code)) continue;

            if (await _db.Awjuh.AnyAsync(w => w.Code == code, cancellationToken))
            {
                // Synchronize structured relations if missing
                var existing = await _db.Awjuh
                    .Include(w => w.Components)
                    .Include(w => w.Scopes)
                    .Include(w => w.Citations)
                    .FirstAsync(w => w.Code == code, cancellationToken);

                if (routesMap.TryGetValue(existing.RouteCode, out var routeId)) existing.TransmissionRouteId = routeId;
                if (entry.TryGetProperty("ruleGroup", out var rg) && rulesMap.TryGetValue(rg.GetString()!, out var ruleId)) existing.ReadingRuleId = ruleId;

                PopulateStructuredComponentsAndScopes(existing, entry, sourcesMap);
                continue;
            }

            var routeCode = entry.GetProperty("routeCode").GetString()!;
            var wajh = new Wajh
            {
                Code = code,
                NameArabic = entry.GetProperty("nameArabic").GetString()!,
                RouteCode = routeCode,
                TransmissionRouteId = routesMap.GetValueOrDefault(routeCode),
                Category = entry.TryGetProperty("category", out var cat) ? cat.GetString()! : "Tahrirat",
                RuleGroup = entry.TryGetProperty("ruleGroup", out var rgp) ? rgp.GetString() : null,
                ReadingRuleId = entry.TryGetProperty("ruleGroup", out var rgProp) && rulesMap.TryGetValue(rgProp.GetString()!, out var ruleFk) ? ruleFk : null,
                Description = entry.TryGetProperty("scope", out var sc) ? sc.GetString() : null,
                ExcludedScope = entry.TryGetProperty("excludedScope", out var exc) ? exc.GetString() : null,
                SourceBranch = entry.TryGetProperty("sourceBranch", out var sb) ? sb.GetInt32() : null,
                SourceBooks = entry.TryGetProperty("sourceBooks", out var sbs) ? string.Join("، ", sbs.EnumerateArray().Select(b => b.GetString())) : null,
                SourceCitation = string.Join("؛ ", entry.GetProperty("sourceIds").EnumerateArray().Select(id =>
                {
                    var sObj = root.GetProperty("sources").EnumerateArray().FirstOrDefault(s => s.GetProperty("id").GetString() == id.GetString());
                    return sObj.ValueKind != JsonValueKind.Undefined ? (sObj.GetProperty("title").GetString() + " — " + sObj.GetProperty("locator").GetString()) : id.GetString()!;
                })),
                DefinitionJson = entry.GetRawText(),
                ReviewStatus = "PendingReview",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            PopulateStructuredComponentsAndScopes(wajh, entry, sourcesMap);

            wajh.Revisions.Add(new WajhRevision
            {
                RevisionNumber = 1,
                Code = wajh.Code,
                NameArabic = wajh.NameArabic,
                RouteCode = wajh.RouteCode,
                ReviewStatus = wajh.ReviewStatus,
                DefinitionSnapshotJson = SerializeSnapshot(wajh),
                ChangedByUserId = "system_seed",
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeReason = "استيراد أولي من الكتالوج الموثق",
                AffectsSemanticRecitation = true
            });

            _db.Awjuh.Add(wajh);
            countAdded++;
        }

        // 5. Seed Combination Constraints
        if (!await _db.CombinationConstraints.AnyAsync(cancellationToken))
        {
            foreach (var c in root.GetProperty("constraints").EnumerateArray())
            {
                var code = c.GetProperty("code").GetString()!;
                var routeCode = c.GetProperty("routeCode").GetString()!;

                var constraint = new CombinationConstraint
                {
                    Code = code,
                    RouteCode = routeCode,
                    TransmissionRouteId = routesMap.GetValueOrDefault(routeCode),
                    NameArabic = c.GetProperty("nameArabic").GetString()!,
                    ContextArabic = c.GetProperty("contextArabic").GetString()!,
                    ConditionJson = c.GetProperty("condition").GetRawText(),
                    EffectJson = c.GetProperty("effect").GetRawText(),
                    ExplanationArabic = c.GetProperty("explanationArabic").GetString()!,
                    ReviewStatus = "PendingReview",
                    AutomaticEnforcement = c.TryGetProperty("automaticEnforcement", out var ae) && ae.GetBoolean(),
                    MarwMappingStatus = c.TryGetProperty("marwMappingStatus", out var ms) ? ms.GetString()! : "NotMapped"
                };

                if (c.TryGetProperty("sourceIds", out var sids))
                {
                    foreach (var sid in sids.EnumerateArray())
                    {
                        var sidStr = sid.GetString()!;
                        if (sourcesMap.TryGetValue(sidStr, out var srcPk))
                        {
                            constraint.Citations.Add(new EvidenceCitation
                            {
                                ReferenceSourceId = srcPk,
                                QuoteOrNote = constraint.ContextArabic
                            });
                        }
                    }
                }

                _db.CombinationConstraints.Add(constraint);
            }
        }

        // 6. Seed Documentary Coverage Matrix initial records
        if (!await _db.DocumentaryCoverageEntries.AnyAsync(cancellationToken))
        {
            _db.DocumentaryCoverageEntries.AddRange(
                new DocumentaryCoverageEntry
                {
                    TransmissionRouteCode = "azraq-shatibiyya",
                    SourceWorkTitle = "حرز الأماني ووجه التهاني (الشاطبية)",
                    ChapterOrSection = "سورة البقرة",
                    SurahNumber = 2,
                    ScopeContext = "اجتماع مد البدل واللين المهموز",
                    AnalysisStatus = "Revue",
                    EntriesCount = 4,
                    ConstraintsCount = 0,
                    Notes = "الأوجه الأربعة المقروء بها من طريق الشاطبية مدونة وموثقة مع القيود."
                },
                new DocumentaryCoverageEntry
                {
                    TransmissionRouteCode = "asbahani-tayyiba",
                    SourceWorkTitle = "تنقيح كتاب التحرير (الضباع)",
                    ChapterOrSection = "سورة يس",
                    SurahNumber = 36,
                    ScopeContext = "فواتح السور — فروع أول يس 1–6",
                    AnalysisStatus = "AResoudre",
                    EntriesCount = 10,
                    ConstraintsCount = 1,
                    Notes = "الفروع العشرة مستخرجة بدقة؛ يلزم إتمام مطابقة عد الآيات مع مصحف مصدرنا الرسمي."
                },
                new DocumentaryCoverageEntry
                {
                    TransmissionRouteCode = "asbahani-tayyiba",
                    SourceWorkTitle = "تنقيح كتاب التحرير (الضباع)",
                    ChapterOrSection = "أصول متفرقة",
                    SurahNumber = null,
                    ScopeContext = "أحكام هاأنتم، نون القلم، ونخلقكم",
                    AnalysisStatus = "Extraite",
                    EntriesCount = 0,
                    ConstraintsCount = 3,
                    Notes = "قيود تحريرية مستقلة تم استخراجها من الصفحات 181 و 184."
                },
                new DocumentaryCoverageEntry
                {
                    TransmissionRouteCode = "azraq-tayyiba",
                    SourceWorkTitle = "طيبة النشر في القراءات العشر (ابن الجزري)",
                    ChapterOrSection = "كامل القرآن",
                    SurahNumber = null,
                    ScopeContext = "طرق الأزرق من النشر وطيبته",
                    AnalysisStatus = "NonEtudiee",
                    EntriesCount = 0,
                    ConstraintsCount = 0,
                    Notes = "مرحلة مستقبلية؛ لم يبدأ استخراج أوجهها بعد."
                }
            );
        }

        // Record Audit Log for seed execution
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = "system",
            Action = "CATALOG_SEEDED_V1",
            EntityType = "Catalog",
            EntityId = "warsh-catalog-v3",
            NewValuesJson = $"اكتمال تهيئة المرجع المنظم برواية ورش. أضيف {countAdded} وجهاً."
        });

        await _db.SaveChangesAsync(cancellationToken);
        return countAdded;
    }

    private static void PopulateStructuredComponentsAndScopes(Wajh wajh, JsonElement entry, Dictionary<string, int> sourcesMap)
    {
        if (entry.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Object)
        {
            wajh.Components.Clear();
            foreach (var prop in comps.EnumerateObject())
            {
                var valStr = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32().ToString() : prop.Value.GetString() ?? "";
                int? valNum = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : null;
                wajh.Components.Add(new WajhComponent
                {
                    ComponentKey = prop.Name,
                    ValueArabic = valStr,
                    ValueNumeric = valNum
                });
            }
        }

        if (entry.TryGetProperty("application", out var app) && app.ValueKind == JsonValueKind.Object)
        {
            wajh.Scopes.Clear();
            int? sSurah = app.TryGetProperty("sourceSurah", out var ss) ? ss.GetInt32() : null;
            int? aStart = null;
            int? aEnd = null;
            if (app.TryGetProperty("sourceAyahs", out var sayahs) && sayahs.ValueKind == JsonValueKind.Array)
            {
                var arr = sayahs.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                if (arr.Length > 0) aStart = arr[0];
                if (arr.Length > 1) aEnd = arr[1];
            }

            wajh.Scopes.Add(new WajhApplicationScope
            {
                SourceSurah = sSurah,
                SourceAyahStart = aStart,
                SourceAyahEnd = aEnd,
                MarwMappingStatus = app.TryGetProperty("marwMappingStatus", out var ms) ? ms.GetString()! : "NotMapped",
                AutomaticAssignment = app.TryGetProperty("automaticAssignment", out var aa) && aa.GetBoolean()
            });
        }

        if (entry.TryGetProperty("sourceIds", out var sids) && sids.ValueKind == JsonValueKind.Array)
        {
            wajh.Citations.Clear();
            foreach (var sid in sids.EnumerateArray())
            {
                var sidStr = sid.GetString()!;
                if (sourcesMap.TryGetValue(sidStr, out var srcFk))
                {
                    wajh.Citations.Add(new EvidenceCitation
                    {
                        ReferenceSourceId = srcFk,
                        Locator = wajh.SourceCitation
                    });
                }
            }
        }
    }

    private static string SerializeSnapshot(Wajh w)
    {
        return JsonSerializer.Serialize(new
        {
            w.Code,
            w.NameArabic,
            w.RouteCode,
            w.Category,
            w.RuleGroup,
            w.Description,
            w.ExcludedScope,
            w.SourceCitation,
            w.ReviewStatus,
            Components = w.Components.Select(c => new { c.ComponentKey, c.ValueArabic, c.ValueNumeric }),
            Scopes = w.Scopes.Select(s => new { s.SourceSurah, s.SourceAyahStart, s.SourceAyahEnd, s.MarwMappingStatus }),
            Citations = w.Citations.Select(c => new { c.ReferenceSourceId, c.Locator })
        });
    }

    private static void ValidateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, "^[a-z0-9-]{3,50}$"))
        {
            throw new ArgumentException("رمز الوجه غير صالح: يجب أن يتكون من 3 إلى 50 حرفاً لاتينياً صغيراً أو رقماً أو شرطة (a-z0-9-).");
        }
    }

    private static void ValidateRequiredFields(string nameArabic, string routeCode, string description, string sourceCitation)
    {
        if (string.IsNullOrWhiteSpace(nameArabic) || nameArabic.Length > 150)
            throw new ArgumentException("الاسم بالعربية إلزامي ويجب ألا يتجاوز 150 حرفاً.");

        if (string.IsNullOrWhiteSpace(routeCode) || routeCode.Length > 100)
            throw new ArgumentException("رمز الطريق إلزامي ويجب ألا يتجاوز 100 حرف.");

        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new ArgumentException("الوصف ونطاق التطبيق إلزامي ويجب ألا يتجاوز 500 حرف.");

        if (string.IsNullOrWhiteSpace(sourceCitation) || sourceCitation.Length > 2000)
            throw new ArgumentException("التوثيق والمرجع إلزامي ويجب ألا يتجاوز 2000 حرف.");
    }

    private async Task<int> GetSingleUsageCountAsync(int id, CancellationToken cancellationToken)
    {
        var sCount = await _db.AudioSegments.CountAsync(s => s.WajhId == id, cancellationToken);
        var rCount = await _db.Recitations.CountAsync(r => r.WajhId == id, cancellationToken);
        var pCount = await _db.MushafPassages.CountAsync(p => p.WajhId == id, cancellationToken);
        return sCount + rCount + pCount;
    }

    private async Task<Dictionary<int, int>> GetUsageCountsMapAsync(List<int> wajhIds, CancellationToken cancellationToken)
    {
        if (wajhIds.Count == 0) return new Dictionary<int, int>();

        var segmentCounts = await _db.AudioSegments
            .AsNoTracking()
            .Where(s => s.WajhId != null && wajhIds.Contains(s.WajhId.Value))
            .GroupBy(s => s.WajhId!.Value)
            .Select(g => new { WajhId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WajhId, x => x.Count, cancellationToken);

        var recitationCounts = await _db.Recitations
            .AsNoTracking()
            .Where(r => r.WajhId != null && wajhIds.Contains(r.WajhId.Value))
            .GroupBy(r => r.WajhId!.Value)
            .Select(g => new { WajhId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WajhId, x => x.Count, cancellationToken);

        var passageCounts = await _db.MushafPassages
            .AsNoTracking()
            .Where(p => p.WajhId != null && wajhIds.Contains(p.WajhId.Value))
            .GroupBy(p => p.WajhId!.Value)
            .Select(g => new { WajhId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WajhId, x => x.Count, cancellationToken);

        var map = new Dictionary<int, int>();
        foreach (var id in wajhIds)
        {
            var total = segmentCounts.GetValueOrDefault(id, 0)
                      + recitationCounts.GetValueOrDefault(id, 0)
                      + passageCounts.GetValueOrDefault(id, 0);
            map[id] = total;
        }
        return map;
    }

    private static WajhDto MapToDto(Wajh w, int usageCount)
    {
        return new WajhDto
        {
            Id = w.Id,
            Code = w.Code,
            NameArabic = w.NameArabic,
            Description = w.Description,
            SortOrder = w.SortOrder,
            RouteCode = w.RouteCode,
            TransmissionRouteName = w.TransmissionRoute?.NameArabic ?? w.RouteCode,
            Category = w.Category,
            RuleGroup = w.RuleGroup,
            ExcludedScope = w.ExcludedScope,
            SourceBranch = w.SourceBranch,
            SourceBooks = w.SourceBooks,
            ReviewStatus = w.ReviewStatus,
            ReviewerUserId = w.ReviewerUserId,
            ReviewedAt = w.ReviewedAt,
            RejectionReason = w.RejectionReason,
            SourceCitation = w.SourceCitation,
            DefinitionJson = w.DefinitionJson,
            Version = w.Version,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
            UsageCount = usageCount,
            Components = w.Components.Select(c => new WajhComponentDto(c.ComponentKey, c.ValueArabic, c.ValueNumeric)).ToList(),
            Scopes = w.Scopes.Select(s => new WajhScopeDto(s.SourceSurah, s.SourceAyahStart, s.SourceAyahEnd, s.MarwMappingStatus, s.AutomaticAssignment, s.ContextDescription)).ToList(),
            Citations = w.Citations.Select(c => new EvidenceCitationDto(c.ReferenceSourceId, c.ReferenceSource?.SourceCode ?? "", c.ReferenceSource?.Title ?? "", c.Locator, c.QuoteOrNote)).ToList(),
            Revisions = w.Revisions.OrderByDescending(r => r.RevisionNumber).Select(r => new WajhRevisionDto(r.RevisionNumber, r.NameArabic, r.ReviewStatus, r.ChangedByUserId, r.ChangedAt, r.ChangeReason)).ToList()
        };
    }
}
