using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class AnnotationService : IAnnotationService
{
    private readonly QuranDbContext _db;
    private readonly ILogger<AnnotationService> _logger;
    private readonly ICorpusResolutionService _corpusService;

    public AnnotationService(
        QuranDbContext db,
        ILogger<AnnotationService> logger,
        ICorpusResolutionService corpusService)
    {
        _db = db;
        _logger = logger;
        _corpusService = corpusService;
    }

    public async Task<AudioSegmentDto> SaveOccurrenceAsync(SaveOccurrenceRequest request, string userId, CancellationToken ct = default)
    {
        var sourceAudio = await _db.SourceAudios
            .FirstOrDefaultAsync(s => s.Id == request.SourceAudioId, ct);

        if (sourceAudio == null)
            throw new ArgumentException("المصدر الصوتي المحدد غير موجود.");
        if (request.Edition == MarwPilotCorpus.EditionCode)
        {
            var reference = (request.Reference ?? "").Split(':');
            if (reference.Length != 2 || !int.TryParse(reference[0], out var sn) || !int.TryParse(reference[1], out var an)
                || !await _db.CorpusAyahs.AnyAsync(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == sn && a.AyahNumber == an, ct))
                throw new ArgumentException("مرجع MARW غير صالح.");
            var sourceSurah = await _db.Surahs.Where(s => s.Id == sourceAudio.SurahId).Select(s => s.Number).FirstOrDefaultAsync(ct);
            if (sourceSurah != sn) throw new ArgumentException("المرجع لا ينتمي إلى سورة المصدر الصوتي.");
            request = request with { AyahId = null, SourceHash = sourceAudio.ChecksumSha256 };
        }

        if (request.StartMs < 0 || request.EndMs <= request.StartMs)
            throw new ArgumentException("حدود المقطع الزمني غير صالحة.");

        if (sourceAudio.DurationMs > 0 && request.EndMs > sourceAudio.DurationMs + 500)
            throw new ArgumentException("نهاية المقطع تتجاوز مدة الملف الصوتي.");

        var id = request.Id ?? Guid.NewGuid();
        var existing = await _db.AudioSegments
            .Include(s => s.Revisions)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (existing != null)
        {
            if (existing.SourceAudioId != request.SourceAudioId)
                throw new ArgumentException("لا يمكن نقل المقطع إلى مصدر صوتي آخر.");
            if (!request.ClientVersion.HasValue)
                throw new DbUpdateConcurrencyException("إصدار المقطع مطلوب قبل التعديل.");
            if (request.ClientVersion.HasValue && existing.Version != request.ClientVersion.Value)
            {
                throw new DbUpdateConcurrencyException("تم تعديل هذا المقطع من قبل مستخدم آخر أو جلسة أخرى. يرجى تحديث البيانات.");
            }

            existing.StartMs = request.StartMs;
            existing.EndMs = request.EndMs;
            existing.Reference = request.Reference;
            existing.AyahId = request.AyahId;
            existing.WajhId = request.WajhId;
            existing.SegmentType = request.SegmentType;
            existing.RepeatOfId = request.RepeatOfId;
            existing.Notes = request.Notes;
            existing.Edition = request.Edition ?? existing.Edition;
            existing.SourceHash = request.SourceHash ?? existing.SourceHash;
            existing.ObservationJson = request.ObservationJson ?? existing.ObservationJson;
            existing.UpdatedByUserId = userId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.Version++;
            existing.RowVersion = Guid.NewGuid().ToByteArray();
            existing.Status = AudioSegmentStatus.Draft;
            existing.PublishedAudioAssetId = null;
            existing.ApprovedAt = null;
            existing.ApprovedByUserId = null;
            existing.ReviewedAt = null;
            existing.ReviewedByUserId = null;
            existing.ValidatedAt = null;
            existing.ValidatedByUserId = null;

            var nextRev = existing.Revisions.Count + 1;
            var rev = new SegmentRevision
            {
                AudioSegmentId = existing.Id,
                RevisionNumber = nextRev,
                StartMs = existing.StartMs,
                EndMs = existing.EndMs,
                Reference = existing.Reference,
                SegmentType = existing.SegmentType,
                WajhId = existing.WajhId,
                ProofNotes = existing.Notes,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.SegmentRevisions.Add(rev);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "UpdateOccurrence",
                EntityType = "AudioSegment",
                EntityId = existing.Id.ToString(),
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { existing.StartMs, existing.EndMs, existing.Reference, existing.Version })
            });

            await _db.SaveChangesAsync(ct);
            return ToDto(existing);
        }
        else
        {
            var segment = new AudioSegment
            {
                Id = id,
                SourceAudioId = request.SourceAudioId,
                StartMs = request.StartMs,
                EndMs = request.EndMs,
                Reference = request.Reference,
                AyahId = request.AyahId,
                WajhId = request.WajhId,
                SegmentType = request.SegmentType,
                RepeatOfId = request.RepeatOfId,
                Notes = request.Notes,
                Edition = request.Edition,
                SourceHash = request.SourceHash,
                ObservationJson = request.ObservationJson,
                Status = AudioSegmentStatus.Draft,
                Version = 1,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            var rev = new SegmentRevision
            {
                AudioSegmentId = segment.Id,
                RevisionNumber = 1,
                StartMs = segment.StartMs,
                EndMs = segment.EndMs,
                Reference = segment.Reference,
                SegmentType = segment.SegmentType,
                WajhId = segment.WajhId,
                ProofNotes = segment.Notes,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.AudioSegments.Add(segment);
            _db.SegmentRevisions.Add(rev);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CreateOccurrence",
                EntityType = "AudioSegment",
                EntityId = segment.Id.ToString(),
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { segment.StartMs, segment.EndMs, segment.Reference, segment.Version })
            });

            await _db.SaveChangesAsync(ct);
            return ToDto(segment);
        }
    }

    public async Task<CaptureEndAyahResult> CaptureEndAyahAsync(CaptureEndAyahRequest request, string userId, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct) : null;
        if (_db.Database.IsNpgsql())
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({request.SourceAudioId.ToString()}))", ct);
        var startMs = request.CurrentStartMs;
        var endMs = request.CapturedTimeMs;
        if (request.SegmentType is not (AudioSegmentType.FullAyah or AudioSegmentType.PartialAyah or AudioSegmentType.Continuation))
            throw new ArgumentException("نوع المقطع غير صالح لهذه العملية.");

        if (endMs <= startMs + 200)
            throw new ArgumentException("الفاصل الزمني للآية قصير جداً (أقل من 200 ميلي ثانية).");

        var partsForValidation = request.CurrentReference.Split(':');
        CorpusAyah? nextCanonical = null;
        var isMarw = request.Edition == MarwPilotCorpus.EditionCode;
        if (isMarw)
        {
            if (partsForValidation.Length != 2 || !int.TryParse(partsForValidation[0], out var sn) || !int.TryParse(partsForValidation[1], out var an))
                throw new ArgumentException("مرجع الآية غير صالح.");
            if (!await _db.CorpusAyahs.AnyAsync(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == sn && a.AyahNumber == an, ct))
                throw new ArgumentException("الآية غير موجودة في مصحف MARW.");
            nextCanonical = await _db.CorpusAyahs.Where(a => a.Corpus!.Code == CorpusResolutionService.MarwCorpusCode && a.SurahNumber == sn && a.AyahNumber > an)
                .OrderBy(a => a.AyahNumber).FirstOrDefaultAsync(ct);
        }
        Guid? captureId = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null :
            new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{userId}|{request.SourceAudioId}|{request.IdempotencyKey}"))[..16]);
        var prior = captureId.HasValue ? await _db.AudioSegments.FindAsync(new object[] { captureId.Value }, ct) : null;
        if (prior != null && (prior.StartMs != startMs || prior.EndMs != endMs || prior.Reference != request.CurrentReference || prior.SegmentType != request.SegmentType))
            throw new DbUpdateConcurrencyException("مفتاح الحفظ مستخدم لمقطع مختلف.");
        var saveReq = new SaveOccurrenceRequest(
            captureId,
            request.SourceAudioId,
            startMs,
            endMs,
            request.CurrentReference,
            isMarw ? null : request.CurrentAyahId,
            request.WajhId,
            request.SegmentType,
            null,
            request.Notes,
            request.Edition,
            request.SourceHash,
            null,
            null
        );

        var saved = prior == null ? await SaveOccurrenceAsync(saveReq, userId, ct) : ToDto(prior);

        // Compute next ayah
        var parts = request.CurrentReference.Split(':');
        int surahNum = 1;
        int ayahNum = 1;
        if (parts.Length == 2 && int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var a))
        {
            surahNum = s;
            ayahNum = a;
        }

        var surah = await _db.Surahs.FirstOrDefaultAsync(su => su.Number == surahNum, ct);
        int nextAyah = ayahNum + 1;
        int nextSurah = surahNum;
        bool hasMore = true;

        if (surah != null && nextAyah > surah.AyahCount)
        {
            hasMore = false;
        }
        if (isMarw)
        {
            hasMore = nextCanonical != null;
            nextAyah = nextCanonical?.AyahNumber ?? ayahNum;
        }
        if (request.SegmentType == AudioSegmentType.PartialAyah)
        {
            hasMore = true;
            nextAyah = ayahNum;
        }

        var nextRef = hasMore ? $"{nextSurah}:{nextAyah}" : null;

        // Automatically update persistent session
        var session = await _db.AnnotationSessions
            .FirstOrDefaultAsync(ss => ss.UserId == userId && ss.SourceAudioId == request.SourceAudioId, ct);

        if (prior != null)
        {
            if (transaction != null) await transaction.CommitAsync(ct);
            return new CaptureEndAyahResult(saved, endMs, nextRef, nextSurah, hasMore ? nextAyah : ayahNum, hasMore);
        }
        if (session == null)
        {
            session = new AnnotationSession
            {
                UserId = userId,
                SourceAudioId = request.SourceAudioId,
                CurrentTimeMs = endMs,
                CurrentSurahNumber = nextSurah,
                CurrentAyahNumber = hasMore ? nextAyah : ayahNum,
                LastSegmentId = saved.Id,
                IsRepeatMode = false,
                RepeatTargetSegmentId = null,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.AnnotationSessions.Add(session);
        }
        else
        {
            session.CurrentTimeMs = endMs;
            session.CurrentSurahNumber = nextSurah;
            session.CurrentAyahNumber = hasMore ? nextAyah : ayahNum;
            session.LastSegmentId = saved.Id;
            session.IsRepeatMode = false;
            session.RepeatTargetSegmentId = null;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);

        return new CaptureEndAyahResult(
            saved,
            endMs,
            nextRef,
            nextSurah,
            hasMore ? nextAyah : ayahNum,
            hasMore
        );
    }

    public async Task<AudioSegmentDto> CaptureRepeatAsync(CaptureRepeatRequest request, string userId, CancellationToken ct = default)
    {
        var target = await _db.AudioSegments
            .FirstOrDefaultAsync(s => s.Id == request.TargetSegmentId, ct);

        if (target == null)
            throw new ArgumentException("المقطع المستهدف للتكرار غير موجود.");
        if (target.SourceAudioId != request.SourceAudioId)
            throw new ArgumentException("يجب أن يكون التكرار من المصدر الصوتي نفسه.");

        if (request.StartMs < target.StartMs)
            throw new ArgumentException("بداية التكرار لا يمكن أن تسبق بداية المقطع الأصلي.");

        var saveReq = new SaveOccurrenceRequest(
            null,
            request.SourceAudioId,
            request.StartMs,
            request.EndMs,
            target.Reference,
            target.AyahId,
            request.WajhId,
            AudioSegmentType.Repetition,
            target.Id,
            request.Notes ?? $"تكرار للآية {target.Reference}",
            request.Edition ?? target.Edition,
            request.SourceHash ?? target.SourceHash,
            null,
            null
        );

        var saved = await SaveOccurrenceAsync(saveReq, userId, ct);

        // Update session time & last segment, but keep current ayah number!
        var session = await _db.AnnotationSessions
            .FirstOrDefaultAsync(ss => ss.UserId == userId && ss.SourceAudioId == request.SourceAudioId, ct);

        if (session != null)
        {
            session.CurrentTimeMs = request.EndMs;
            session.LastSegmentId = saved.Id;
            session.IsRepeatMode = false;
            session.RepeatTargetSegmentId = null;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return saved;
    }

    public async Task<IReadOnlyList<AudioSegmentDto>> GetOccurrencesForSourceAsync(Guid sourceAudioId, CancellationToken ct = default)
    {
        var segments = await _db.AudioSegments
            .Include(s => s.Ayah)
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .AsNoTracking()
            .Where(s => s.SourceAudioId == sourceAudioId)
            .OrderBy(s => s.StartMs)
            .ThenBy(s => s.SequenceNumber)
            .ToListAsync(ct);

        return segments.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AudioSegmentDto>> GetOccurrencesForReferenceAsync(string edition, string reference, bool publishedOnly, CancellationToken ct = default)
    {
        var query = _db.AudioSegments
            .Include(s => s.Ayah)
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .AsNoTracking()
            .Where(s => s.Reference == reference);

        if (!string.IsNullOrEmpty(edition))
        {
            query = query.Where(s => s.Edition == edition);
        }

        if (publishedOnly)
        {
            query = query.Where(s => s.Status == AudioSegmentStatus.Published);
        }

        var segments = await query
            .OrderBy(s => s.StartMs)
            .ToListAsync(ct);

        return segments.Select(ToDto).ToList();
    }

    public async Task<AnnotationSessionDto> GetOrCreateSessionAsync(string userId, Guid sourceAudioId, CancellationToken ct = default)
    {
        var session = await _db.AnnotationSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SourceAudioId == sourceAudioId, ct);

        if (session == null)
        {
            session = new AnnotationSession
            {
                UserId = userId,
                SourceAudioId = sourceAudioId,
                CurrentTimeMs = 0,
                CurrentSurahNumber = 1,
                CurrentAyahNumber = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.AnnotationSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        return new AnnotationSessionDto(
            session.Id,
            session.UserId,
            session.SourceAudioId,
            session.CurrentTimeMs,
            session.CurrentSurahNumber,
            session.CurrentAyahNumber,
            session.LastSegmentId,
            session.IsRepeatMode,
            session.RepeatTargetSegmentId,
            session.UpdatedAt
        );
    }

    public async Task<AnnotationSessionDto> UpdateSessionAsync(UpdateSessionRequest request, string userId, CancellationToken ct = default)
    {
        var session = await _db.AnnotationSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SourceAudioId == request.SourceAudioId, ct);

        if (session == null)
        {
            session = new AnnotationSession
            {
                UserId = userId,
                SourceAudioId = request.SourceAudioId,
                CurrentTimeMs = request.CurrentTimeMs,
                CurrentSurahNumber = request.CurrentSurahNumber,
                CurrentAyahNumber = request.CurrentAyahNumber,
                LastSegmentId = request.LastSegmentId,
                IsRepeatMode = request.IsRepeatMode,
                RepeatTargetSegmentId = request.RepeatTargetSegmentId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.AnnotationSessions.Add(session);
        }
        else
        {
            session.CurrentTimeMs = request.CurrentTimeMs;
            session.CurrentSurahNumber = request.CurrentSurahNumber;
            session.CurrentAyahNumber = request.CurrentAyahNumber;
            session.LastSegmentId = request.LastSegmentId;
            session.IsRepeatMode = request.IsRepeatMode;
            session.RepeatTargetSegmentId = request.RepeatTargetSegmentId;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new AnnotationSessionDto(
            session.Id,
            session.UserId,
            session.SourceAudioId,
            session.CurrentTimeMs,
            session.CurrentSurahNumber,
            session.CurrentAyahNumber,
            session.LastSegmentId,
            session.IsRepeatMode,
            session.RepeatTargetSegmentId,
            session.UpdatedAt
        );
    }

    public async Task<OccurrenceMigrationReport> MigrateMushafPassagesToAudioSegmentsAsync(CancellationToken ct = default)
    {
        var passages = await _db.MushafPassages
            .AsNoTracking()
            .ToListAsync(ct);

        var details = new List<string>();
        var errors = new List<string>();
        int migrated = 0;
        int existing = 0;

        foreach (var passage in passages)
        {
            try
            {
                var existingSegment = await _db.AudioSegments
                    .FirstOrDefaultAsync(s => s.Id == passage.Id, ct);

                if (existingSegment != null)
                {
                    existing++;
                    details.Add($"المقطع {passage.Id} ({passage.Reference}) موجود مسبقاً في AudioSegments وتم الإبقاء عليه.");
                    continue;
                }

                // Resolve SourceAudio
                var source = await _db.SourceAudios
                    .FirstOrDefaultAsync(s => s.StorageKey == passage.SourceKey || s.ChecksumSha256 == passage.SourceHash, ct);

                if (source == null)
                {
                    // Fallback to first sourceAudio or create stub for migration integrity
                    source = await _db.SourceAudios.FirstOrDefaultAsync(ct);
                }

                if (source == null)
                {
                    errors.Add($"تعذر العثور على مصدر صوتي مطابق للمقطع {passage.Id} (مفتاح: {passage.SourceKey}).");
                    continue;
                }

                var status = AudioSegmentStatus.Draft; // Import never certifies or publishes an occurrence.
                var segmentType = (AudioSegmentType)passage.SegmentType;

                var segment = new AudioSegment
                {
                    Id = passage.Id, // Preserve exact GUID
                    SourceAudioId = source.Id,
                    Reference = passage.Reference,
                    Edition = passage.Edition,
                    SourceHash = passage.SourceHash,
                    StartMs = passage.StartMs,
                    EndMs = passage.EndMs,
                    SegmentType = segmentType,
                    RepeatOfId = passage.RepeatOfId,
                    WajhId = passage.WajhId,
                    Notes = passage.Notes,
                    ObservationJson = passage.ObservationJson,
                    Status = status,
                    Version = passage.Version,
                    CreatedByUserId = "Migration",
                    CreatedAt = passage.UpdatedAt,
                    RowVersion = Guid.NewGuid().ToByteArray()
                };

                var rev = new SegmentRevision
                {
                    AudioSegmentId = segment.Id,
                    RevisionNumber = 1,
                    StartMs = segment.StartMs,
                    EndMs = segment.EndMs,
                    Reference = segment.Reference,
                    SegmentType = segment.SegmentType,
                    WajhId = segment.WajhId,
                    ProofNotes = segment.Notes,
                    CreatedByUserId = "Migration",
                    CreatedAt = passage.UpdatedAt
                };

                _db.AudioSegments.Add(segment);
                _db.SegmentRevisions.Add(rev);

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = "Migration",
                    Action = "MigrateFromMushafPassage",
                    EntityType = "AudioSegment",
                    EntityId = segment.Id.ToString(),
                    NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { segment.StartMs, segment.EndMs, segment.Reference })
                });

                migrated++;
                details.Add($"تم ترحيل المقطع {passage.Id} بنجاح: مرجع {passage.Reference}، البداية {passage.StartMs}ms والنهاية {passage.EndMs}ms.");
            }
            catch (Exception ex)
            {
                errors.Add($"خطأ أثناء ترحيل المقطع {passage.Id}: {ex.Message}");
            }
        }

        if (migrated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Migrated {Count} MushafPassages to AudioSegments", migrated);
        }

        return new OccurrenceMigrationReport(migrated, existing, details, errors);
    }

    public async Task<bool> DeleteOccurrenceAsync(Guid id, int clientVersion, string userId, CancellationToken ct = default)
    {
        var segment = await _db.AudioSegments.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (segment == null) return false;

        if (segment.Version != clientVersion)
        {
            throw new DbUpdateConcurrencyException("تم تعديل هذا المقطع من قبل مستخدم آخر. يرجى تحديث البيانات.");
        }

        if (await _db.AudioSegments.AnyAsync(s => s.RepeatOfId == id, ct))
        {
            throw new InvalidOperationException("لا يمكن حذف هذا المقطع لوجود مقاطع إعادة مرتبطة به. احذف أو عدل مقاطع الإعادة أولاً.");
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "DeleteOccurrence",
            EntityType = "AudioSegment",
            EntityId = segment.Id.ToString(),
            OldValuesJson = System.Text.Json.JsonSerializer.Serialize(new { segment.StartMs, segment.EndMs, segment.Reference })
        });

        _db.AudioSegments.Remove(segment);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UndoCaptureResult> UndoLastCaptureAsync(Guid sourceAudioId, string userId, CancellationToken ct = default)
    {
        var session = await _db.AnnotationSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SourceAudioId == sourceAudioId, ct);

        var lastSegment = await _db.AudioSegments
            .Where(s => s.SourceAudioId == sourceAudioId && s.Status == AudioSegmentStatus.Draft)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastSegment == null)
        {
            return new UndoCaptureResult(
                Success: false,
                RemovedSegmentId: Guid.Empty,
                RestoredTimeMs: session?.CurrentTimeMs ?? 0,
                RestoredSurahNumber: session?.CurrentSurahNumber ?? 1,
                RestoredAyahNumber: session?.CurrentAyahNumber ?? 1,
                RestoredReference: null,
                Message: "لا توجد مقاطع حديثة يمكن التراجع عنها."
            );
        }

        if (await _db.AudioSegments.AnyAsync(s => s.RepeatOfId == lastSegment.Id, ct))
        {
            return new UndoCaptureResult(
                Success: false,
                RemovedSegmentId: lastSegment.Id,
                RestoredTimeMs: session?.CurrentTimeMs ?? 0,
                RestoredSurahNumber: session?.CurrentSurahNumber ?? 1,
                RestoredAyahNumber: session?.CurrentAyahNumber ?? 1,
                RestoredReference: lastSegment.Reference,
                Message: "لا يمكن التراجع عن هذا المقطع لوجود مقاطع تكرار مرتبطة به."
            );
        }

        var removedId = lastSegment.Id;
        var restoredTimeMs = lastSegment.StartMs;
        int restoredSurah = session?.CurrentSurahNumber ?? 1;
        int restoredAyah = session?.CurrentAyahNumber ?? 1;
        string? restoredRef = lastSegment.Reference;

        if (!string.IsNullOrEmpty(lastSegment.Reference))
        {
            var parts = lastSegment.Reference.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var a))
            {
                restoredSurah = s;
                restoredAyah = a;
            }
        }

        var revisions = await _db.SegmentRevisions.Where(r => r.AudioSegmentId == removedId).ToListAsync(ct);
        _db.SegmentRevisions.RemoveRange(revisions);
        _db.AudioSegments.Remove(lastSegment);

        if (session != null)
        {
            session.CurrentTimeMs = restoredTimeMs;
            session.CurrentSurahNumber = restoredSurah;
            session.CurrentAyahNumber = restoredAyah;
            session.LastSegmentId = null;
            session.IsRepeatMode = false;
            session.RepeatTargetSegmentId = null;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "UndoCapture",
            EntityType = "AudioSegment",
            EntityId = removedId.ToString(),
            OldValuesJson = System.Text.Json.JsonSerializer.Serialize(new { lastSegment.StartMs, lastSegment.EndMs, lastSegment.Reference })
        });

        await _db.SaveChangesAsync(ct);

        return new UndoCaptureResult(
            Success: true,
            RemovedSegmentId: removedId,
            RestoredTimeMs: restoredTimeMs,
            RestoredSurahNumber: restoredSurah,
            RestoredAyahNumber: restoredAyah,
            RestoredReference: restoredRef,
            Message: $"تم التراجع عن تسجيل الآية {restoredRef} واستعادة الموضع {restoredTimeMs}ms بنجاح."
        );
    }

    private static AudioSegmentDto ToDto(AudioSegment s)
    {
        return new AudioSegmentDto(
            s.Id,
            s.SourceAudioId,
            s.AyahId,
            s.Ayah != null ? (int?)s.Ayah.AyahNumber : null,
            s.WajhId,
            s.Wajh?.Code,
            s.Wajh?.NameArabic,
            s.StartMs,
            s.EndMs,
            s.StartWordIndex,
            s.EndWordIndex,
            s.SegmentType,
            s.SequenceNumber,
            s.Status,
            s.Notes,
            s.PublishedAudioAsset?.StorageKey != null ? $"/audio/{s.PublishedAudioAsset.StorageKey}" : null,
            s.RowVersion != null ? Convert.ToBase64String(s.RowVersion) : "",
            s.CreatedAt, s.RejectionReason, s.ReviewedByUserId, s.ReviewedAt, s.ApprovedByUserId, s.ApprovedAt, s.Version, s.Reference, s.Edition
        );
    }
}
