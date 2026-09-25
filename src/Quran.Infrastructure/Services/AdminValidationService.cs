using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class AdminValidationService : IAdminValidationService
{
    private readonly QuranDbContext _dbContext;
    private readonly ILogger<AdminValidationService> _logger;

    public AdminValidationService(QuranDbContext dbContext, ILogger<AdminValidationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminDashboardDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalSurahs = await _dbContext.Surahs.CountAsync(cancellationToken);
        var totalAyahs = await _dbContext.Ayahs.CountAsync(cancellationToken);

        var ayahsWithAudio = await _dbContext.Ayahs
            .Where(a => a.Recitations.Any(r => r.IsPublished && r.Status == RecitationStatus.Published))
            .CountAsync(cancellationToken);

        var ayahsWithoutAudio = totalAyahs - ayahsWithAudio;

        var pendingValidation = await _dbContext.Recitations
            .Where(r => r.Status == RecitationStatus.Imported || r.Status == RecitationStatus.Segmented || r.Status == RecitationStatus.Reviewed)
            .CountAsync(cancellationToken);

        var published = await _dbContext.Recitations
            .Where(r => r.IsPublished && r.Status == RecitationStatus.Published)
            .CountAsync(cancellationToken);

        var rejected = await _dbContext.Recitations
            .Where(r => r.Status == RecitationStatus.Rejected)
            .CountAsync(cancellationToken);

        return new AdminDashboardDto(
            TotalSurahs: totalSurahs,
            TotalAyahs: totalAyahs,
            AyahsWithAudio: ayahsWithAudio,
            AyahsWithoutAudio: ayahsWithoutAudio,
            SegmentsPendingValidation: pendingValidation,
            SegmentsPublished: published,
            SegmentsRejected: rejected
        );
    }

    public async Task<IReadOnlyList<AdminRecitationItemDto>> GetRecitationsForValidationAsync(
        int? surahNumber,
        RecitationStatus? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Recitations
            .AsNoTracking()
            .Include(r => r.Ayah)
                .ThenInclude(a => a.Surah)
            .Include(r => r.Wajh)
            .Include(r => r.AudioAsset)
            .AsQueryable();

        if (surahNumber.HasValue)
        {
            query = query.Where(r => r.Ayah.Surah.Number == surahNumber.Value);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(r => r.Status == statusFilter.Value);
        }

        var list = await query
            .OrderBy(r => r.Ayah.Surah.Number)
            .ThenBy(r => r.Ayah.AyahNumber)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return list.Select(r => new AdminRecitationItemDto(
            RecitationId: r.Id,
            SurahNumber: r.Ayah.Surah.Number,
            SurahNameArabic: r.Ayah.Surah.NameArabic,
            AyahNumber: r.Ayah.AyahNumber,
            TextWarsh: r.Ayah.TextWarsh,
            WajhId: r.WajhId,
            WajhCode: r.Wajh?.Code,
            WajhNameArabic: r.Wajh?.NameArabic,
            AudioUrl: $"/audio/{r.AudioAsset.StorageKey}",
            DurationMs: r.AudioAsset.DurationMs,
            SizeBytes: r.AudioAsset.SizeBytes,
            Status: r.Status.ToString(),
            IsPublished: r.IsPublished,
            CreatedAt: r.CreatedAt
        )).ToList();
    }

    public async Task ApproveRecitationAsync(long recitationId, string? comment, string? validatorUserId, CancellationToken cancellationToken = default)
    {
        var recitation = await _dbContext.Recitations.FindAsync(new object[] { recitationId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Récitation #{recitationId} non trouvée.");

        recitation.Status = RecitationStatus.Approved;
        recitation.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.ValidationReviews.Add(new ValidationReview
        {
            RecitationId = recitationId,
            Status = RecitationStatus.Approved,
            Comment = comment ?? "Segment approuvé après vérification.",
            ValidatorUserId = validatorUserId ?? "admin",
            ValidatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recitation #{Id} approved by {User}", recitationId, validatorUserId);
    }

    public async Task PublishRecitationAsync(long recitationId, string? comment, string? validatorUserId, CancellationToken cancellationToken = default)
    {
        var recitation = await _dbContext.Recitations.FindAsync(new object[] { recitationId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Récitation #{recitationId} non trouvée.");
        if (recitation.WajhId.HasValue && !await _dbContext.Awjuh.AnyAsync(w => w.Id == recitation.WajhId && w.ReviewStatus == "Approved", cancellationToken))
            throw new InvalidOperationException("Le wajh doit être approuvé dans le référentiel avant publication.");
        // Historical Recitations are a projection, never an independent publishing workflow.
        if (!await _dbContext.AudioSegments.AnyAsync(s => s.PublishedAudioAssetId == recitation.AudioAssetId
            && s.Status == AudioSegmentStatus.Published && s.Edition == MarwPilotCorpus.EditionCode, cancellationToken))
            throw new InvalidOperationException("Publier d'abord l'occurrence qualifiée depuis la file de revue. La publication historique directe est désactivée.");

        recitation.Status = RecitationStatus.Published;
        recitation.IsPublished = true;
        recitation.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.ValidationReviews.Add(new ValidationReview
        {
            RecitationId = recitationId,
            Status = RecitationStatus.Published,
            Comment = comment ?? "Segment publié publiquement.",
            ValidatorUserId = validatorUserId ?? "admin",
            ValidatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recitation #{Id} published by {User}", recitationId, validatorUserId);
    }

    public async Task RejectRecitationAsync(long recitationId, string comment, string? validatorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Un commentaire expliquant le rejet est obligatoire.", nameof(comment));

        var recitation = await _dbContext.Recitations.FindAsync(new object[] { recitationId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Récitation #{recitationId} non trouvée.");

        recitation.Status = RecitationStatus.Rejected;
        recitation.IsPublished = false;
        recitation.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.ValidationReviews.Add(new ValidationReview
        {
            RecitationId = recitationId,
            Status = RecitationStatus.Rejected,
            Comment = comment,
            ValidatorUserId = validatorUserId ?? "admin",
            ValidatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recitation #{Id} rejected by {User}: {Comment}", recitationId, validatorUserId, comment);
    }

    public async Task UpdateWajhAsync(long recitationId, int wajhId, CancellationToken cancellationToken = default)
    {
        var recitation = await _dbContext.Recitations.FindAsync(new object[] { recitationId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Récitation #{recitationId} non trouvée.");

        var wajhExists = await _dbContext.Awjuh.AnyAsync(w => w.Id == wajhId, cancellationToken);
        if (!wajhExists)
            throw new ArgumentException($"Wajh #{wajhId} inexistant.", nameof(wajhId));

        recitation.WajhId = wajhId;
        recitation.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recitation #{Id} Wajh updated to #{WajhId}", recitationId, wajhId);
    }

    public async Task<IReadOnlyList<Wajh>> GetAvailableAwjuhAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Awjuh
            .Where(w => w.ReviewStatus != "Archived")
            .AsNoTracking()
            .OrderBy(w => w.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
