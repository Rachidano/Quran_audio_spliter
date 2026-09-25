using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public partial class AudioEditorService : IAudioEditorService
{
    private readonly QuranDbContext _db;
    private readonly IStorageService _storage;
    private readonly IAudioProcessorService _audioProcessor;
    private readonly ILogger<AudioEditorService> _logger;

    [GeneratedRegex(@"[\u064B-\u065F\u0670\u06D6-\u06ED]")]
    private static partial Regex ArabicDiacriticsRegex();

    [GeneratedRegex(@"[إأآٱ]")]
    private static partial Regex ArabicAlefRegex();

    public AudioEditorService(
        QuranDbContext db,
        IStorageService storage,
        IAudioProcessorService audioProcessor,
        ILogger<AudioEditorService> logger)
    {
        _db = db;
        _storage = storage;
        _audioProcessor = audioProcessor;
        _logger = logger;
    }

    public async Task<AudioEditorContextDto> GetEditorContextBySurahAsync(int surahNumber, CancellationToken cancellationToken = default)
    {
        var surah = await _db.Surahs
            .Include(s => s.Ayahs.OrderBy(a => a.AyahNumber))
            .ThenInclude(a => a.Words.OrderBy(w => w.WordIndex))
            .FirstOrDefaultAsync(s => s.Number == surahNumber, cancellationToken);

        if (surah == null)
            throw new KeyNotFoundException($"Sourate {surahNumber} introuvable.");

        // Tokenisation des mots pour les ayats si nécessaire
        bool needSave = false;
        foreach (var ayah in surah.Ayahs)
        {
            if (!ayah.Words.Any())
            {
                TokenizeAyahInternal(ayah);
                needSave = true;
            }
        }
        if (needSave)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Trouver ou initialiser SourceAudio
        var sourceAudio = await _db.SourceAudios
            .Include(sa => sa.Reciter)
            .Include(sa => sa.Riwaya)
            .FirstOrDefaultAsync(sa => sa.SurahId == surah.Id, cancellationToken);

        var relativeKey = $"sources/{surah.Number:D3}/original.mp3";
        var physicalPath = _storage.GetPhysicalPath(relativeKey);

        if (sourceAudio == null)
        {
            long durationMs = 0;
            long sizeBytes = 0;
            int bitrate = 128;
            string checksum = "";
            var status = SourceAudioStatus.Available;

            if (File.Exists(physicalPath))
            {
                try
                {
                    var probe = await _audioProcessor.ProbeAudioAsync(physicalPath, cancellationToken);
                    durationMs = (long)probe.Duration.TotalMilliseconds;
                    sizeBytes = probe.SizeBytes;
                    bitrate = probe.Bitrate;
                    checksum = probe.Checksum;
                    status = SourceAudioStatus.Downloaded;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible de sonder {Path}", physicalPath);
                }
            }

            sourceAudio = new SourceAudio
            {
                Id = Guid.NewGuid(),
                SurahId = surah.Id,
                ReciterId = 1, // Cheikh Saber Abdel-Hakam
                RiwayaId = 1,  // Warsh 'an Nafi'
                OriginalFileName = Path.GetFileName(physicalPath),
                StorageKey = relativeKey,
                OriginalUrl = $"https://archive.org/download/z0603_202406x/{surah.Number:D3}.mp3",
                DurationMs = durationMs,
                SizeBytes = sizeBytes,
                Bitrate = bitrate,
                Codec = "mp3",
                ChecksumSha256 = checksum,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _db.SourceAudios.Add(sourceAudio);
            await _db.SaveChangesAsync(cancellationToken);

            sourceAudio = await _db.SourceAudios
                .Include(sa => sa.Reciter)
                .Include(sa => sa.Riwaya)
                .FirstAsync(sa => sa.Id == sourceAudio.Id, cancellationToken);
        }
        else if (sourceAudio.DurationMs == 0 && File.Exists(physicalPath))
        {
            try
            {
                var probe = await _audioProcessor.ProbeAudioAsync(physicalPath, cancellationToken);
                sourceAudio.DurationMs = (long)probe.Duration.TotalMilliseconds;
                sourceAudio.SizeBytes = probe.SizeBytes;
                sourceAudio.Bitrate = probe.Bitrate;
                sourceAudio.ChecksumSha256 = probe.Checksum;
                sourceAudio.Status = SourceAudioStatus.Downloaded;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur probe pour {Path}", physicalPath);
            }
        }

        // Charger Awjuh
        var awjuh = await _db.Awjuh
            .Where(w => w.ReviewStatus != "Archived")
            .OrderBy(w => w.SortOrder)
            .Select(w => new WajhOptionDto(w.Id, w.Code, w.NameArabic, w.Description))
            .ToListAsync(cancellationToken);

        // Charger AudioSegments
        var segments = await _db.AudioSegments
            .Include(s => s.Ayah)
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .Where(s => s.SourceAudioId == sourceAudio.Id)
            .OrderBy(s => s.StartMs)
            .ThenBy(s => s.SequenceNumber)
            .Select(s => new AudioSegmentDto(
                s.Id,
                s.SourceAudioId,
                s.AyahId,
                s.Ayah != null ? (int?)s.Ayah.AyahNumber : null,
                s.WajhId,
                s.Wajh != null ? s.Wajh.Code : null,
                s.Wajh != null ? s.Wajh.NameArabic : null,
                s.StartMs,
                s.EndMs,
                s.StartWordIndex,
                s.EndWordIndex,
                s.SegmentType,
                s.SequenceNumber,
                s.Status,
                s.Notes,
                s.PublishedAudioAsset != null ? _storage.GetPublicUrl(s.PublishedAudioAsset.StorageKey) : null,
                Convert.ToBase64String(s.RowVersion),
                s.CreatedAt,
                s.RejectionReason,
                s.ReviewedByUserId,
                s.ReviewedAt,
                s.ApprovedByUserId,
                s.ApprovedAt,
                s.Version,
                s.Reference,
                s.Edition
            ))
            .ToListAsync(cancellationToken);

        var ayahDtos = surah.Ayahs.Select(a => new AyahEditorDto(
            a.Id,
            a.SurahId,
            a.AyahNumber,
            a.NumberInQuran,
            a.TextWarsh,
            a.Words.OrderBy(w => w.WordIndex).Select(w => new AyahWordDto(w.Id, w.AyahId, w.WordIndex, w.TextWarsh, w.SearchText)).ToList()
        )).ToList();

        var sourceAudioDto = new SourceAudioDto(
            sourceAudio.Id,
            sourceAudio.SurahId,
            surah.Number,
            surah.NameArabic,
            sourceAudio.ReciterId,
            sourceAudio.Reciter?.NameArabic ?? "صابر عبد الحكم",
            sourceAudio.RiwayaId,
            sourceAudio.Riwaya?.NameArabic ?? "ورش عن نافع",
            sourceAudio.StorageKey,
            _storage.GetPublicUrl(sourceAudio.StorageKey),
            sourceAudio.DurationMs,
            sourceAudio.SizeBytes,
            sourceAudio.Status
        );

        return new AudioEditorContextDto(
            sourceAudioDto,
            surah.Number,
            surah.NameArabic,
            surah.NameLatin,
            surah.AyahCount,
            ayahDtos,
            awjuh,
            segments
        );
    }

    public async Task<AudioSegmentDto> CreateSegmentAsync(CreateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.StartMs < 0)
            throw new ArgumentException("Le timestamp de début doit être positif.");
        if (request.EndMs <= request.StartMs)
            throw new ArgumentException("Le timestamp de fin doit être supérieur au début.");
        if (request.EndMs - request.StartMs < 200)
            throw new ArgumentException("La durée minimale d'un segment est de 200 ms.");

        var sourceAudio = await _db.SourceAudios.FirstOrDefaultAsync(sa => sa.Id == request.SourceAudioId, cancellationToken);
        if (sourceAudio == null)
            throw new KeyNotFoundException("Source audio introuvable.");

        var ayah = await _db.Ayahs.FirstOrDefaultAsync(a => a.Id == request.AyahId, cancellationToken);
        if (ayah == null)
            throw new KeyNotFoundException("Verset introuvable.");

        if (ayah.SurahId != sourceAudio.SurahId)
            throw new InvalidOperationException("L'ayah sélectionnée n'appartient pas à la sourate de la source audio.");

        if (request.StartWordIndex.HasValue && request.EndWordIndex.HasValue)
        {
            if (request.StartWordIndex > request.EndWordIndex)
                throw new ArgumentException("L'index de mot de début ne peut dépasser l'index de mot de fin.");
        }

        // Déterminer le sequence number pour cette ayah
        var maxSeq = await _db.AudioSegments
            .Where(s => s.SourceAudioId == sourceAudio.Id && s.AyahId == request.AyahId)
            .Select(s => (int?)s.SequenceNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var segment = new AudioSegment
        {
            Id = Guid.NewGuid(),
            SourceAudioId = request.SourceAudioId,
            AyahId = request.AyahId,
            WajhId = request.WajhId,
            StartMs = request.StartMs,
            EndMs = request.EndMs,
            StartWordIndex = request.StartWordIndex,
            EndWordIndex = request.EndWordIndex,
            SegmentType = request.SegmentType,
            SequenceNumber = maxSeq + 1,
            Status = AudioSegmentStatus.Draft,
            Notes = request.Notes,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        _db.AudioSegments.Add(segment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Nouveau segment {Id} créé pour la source {SourceAudioId}, ayah {AyahId}",
            segment.Id, segment.SourceAudioId, segment.AyahId);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> UpdateSegmentAsync(Guid id, UpdateAudioSegmentRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.StartMs < 0)
            throw new ArgumentException("Le timestamp de début doit être positif.");
        if (request.EndMs <= request.StartMs)
            throw new ArgumentException("Le timestamp de fin doit être supérieur au début.");
        if (request.EndMs - request.StartMs < 200)
            throw new ArgumentException("La durée minimale d'un segment est de 200 ms.");

        var segment = await _db.AudioSegments
            .Include(s => s.SourceAudio)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        // Vérification de concurrence optimiste
        var currentVersionBase64 = Convert.ToBase64String(segment.RowVersion);
        if (!string.Equals(currentVersionBase64, request.RowVersionBase64, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException("Ce segment a été modifié par un autre processus ou utilisateur. Veuillez recharger.");
        }

        var ayah = await _db.Ayahs.FirstOrDefaultAsync(a => a.Id == request.AyahId, cancellationToken);
        if (ayah == null)
            throw new KeyNotFoundException("Verset introuvable.");

        if (ayah.SurahId != segment.SourceAudio.SurahId)
            throw new InvalidOperationException("L'ayah sélectionnée n'appartient pas à la sourate correspondante.");

        if (request.StartWordIndex.HasValue && request.EndWordIndex.HasValue)
        {
            if (request.StartWordIndex > request.EndWordIndex)
                throw new ArgumentException("L'index de mot de début ne peut dépasser l'index de mot de fin.");
        }

        bool contentChanged = segment.StartMs != request.StartMs || segment.EndMs != request.EndMs
            || segment.AyahId != request.AyahId || segment.WajhId != request.WajhId
            || segment.StartWordIndex != request.StartWordIndex || segment.EndWordIndex != request.EndWordIndex
            || segment.SegmentType != request.SegmentType;

        segment.AyahId = request.AyahId;
        segment.WajhId = request.WajhId;
        segment.StartMs = request.StartMs;
        segment.EndMs = request.EndMs;
        segment.StartWordIndex = request.StartWordIndex;
        segment.EndWordIndex = request.EndWordIndex;
        segment.SegmentType = request.SegmentType;
        segment.Notes = request.Notes;
        segment.UpdatedByUserId = userId;
        segment.UpdatedAt = DateTime.UtcNow;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        // Si les limites temporelles changent et que le segment était rendu ou publié, repasser en Brouillon pour nécessiter un nouveau rendu
        if (contentChanged)
        {
            segment.Version++;
            segment.Status = AudioSegmentStatus.Draft;
            segment.ValidatedAt = null;
            segment.ValidatedByUserId = null;
            segment.ReviewedAt = null;
            segment.ReviewedByUserId = null;
            segment.ApprovedAt = null;
            segment.ApprovedByUserId = null;
            segment.RejectionReason = null;
            // The previous published asset remains a snapshot, not the new draft.
            segment.PublishedAudioAssetId = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task DeleteSegmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.SegmentImageRegions)
            .Include(s => s.Revisions)
            .Include(s => s.TextSpans)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (segment == null)
            return;

        if (segment.Status == AudioSegmentStatus.Published)
            throw new InvalidOperationException("Impossible de supprimer directement un segment publié. Veuillez d'abord retirer sa publication.");

        // Clean up any session pointing to this segment
        var sessions = await _db.AnnotationSessions
            .Where(s => s.LastSegmentId == id || s.RepeatTargetSegmentId == id)
            .ToListAsync(cancellationToken);
        foreach (var sess in sessions)
        {
            if (sess.LastSegmentId == id) sess.LastSegmentId = null;
            if (sess.RepeatTargetSegmentId == id) sess.RepeatTargetSegmentId = null;
        }

        // Clean up any other segments pointing to this as RepeatOf
        var repeats = await _db.AudioSegments
            .Where(s => s.RepeatOfId == id)
            .ToListAsync(cancellationToken);
        foreach (var r in repeats)
        {
            r.RepeatOfId = null;
        }

        _db.AudioSegmentImageRegions.RemoveRange(segment.SegmentImageRegions);
        _db.SegmentRevisions.RemoveRange(segment.Revisions);
        _db.SegmentTextSpans.RemoveRange(segment.TextSpans);
        _db.AudioSegments.Remove(segment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Segment {Id} supprimé avec succès.", id);
    }

    public async Task ResetSurahAsync(int surahNumber, string userId, CancellationToken cancellationToken = default)
    {
        var sourceAudio = await _db.SourceAudios
            .Include(sa => sa.Surah)
            .FirstOrDefaultAsync(sa => sa.Surah.Number == surahNumber, cancellationToken);
        if (sourceAudio == null)
            throw new KeyNotFoundException($"Source audio pour la sourate {surahNumber} introuvable.");

        var segments = await _db.AudioSegments
            .Include(s => s.SegmentImageRegions)
            .Include(s => s.Revisions)
            .Include(s => s.TextSpans)
            .Where(s => s.SourceAudioId == sourceAudio.Id)
            .ToListAsync(cancellationToken);

        var segmentIds = segments.Select(s => s.Id).ToHashSet();

        // Reset any sessions for this source
        var sessions = await _db.AnnotationSessions
            .Where(s => s.SourceAudioId == sourceAudio.Id || (s.LastSegmentId != null && segmentIds.Contains(s.LastSegmentId.Value)))
            .ToListAsync(cancellationToken);

        foreach (var sess in sessions)
        {
            sess.CurrentTimeMs = 0;
            sess.CurrentSurahNumber = surahNumber;
            sess.CurrentAyahNumber = 1;
            sess.LastSegmentId = null;
            sess.IsRepeatMode = false;
            sess.RepeatTargetSegmentId = null;
            sess.UpdatedAt = DateTimeOffset.UtcNow;
        }

        foreach (var s in segments)
        {
            s.RepeatOfId = null;
        }

        foreach (var s in segments)
        {
            _db.AudioSegmentImageRegions.RemoveRange(s.SegmentImageRegions);
            _db.SegmentRevisions.RemoveRange(s.Revisions);
            _db.SegmentTextSpans.RemoveRange(s.TextSpans);
        }

        _db.AudioSegments.RemoveRange(segments);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sourate {SurahNumber} réinitialisée à 0 par {User} ({Count} segments supprimés)",
            surahNumber, userId, segments.Count);
    }

    public async Task<AudioSegmentDto> SubmitForReviewAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        if (segment.Status != AudioSegmentStatus.Draft && segment.Status != AudioSegmentStatus.Rejected)
            throw new InvalidOperationException("Seul un segment au statut Brouillon ou Rejeté peut être soumis pour revue.");

        segment.Status = AudioSegmentStatus.Reviewed;
        segment.ReviewedByUserId = userId;
        segment.ReviewedAt = DateTimeOffset.UtcNow;
        segment.UpdatedByUserId = userId;
        segment.UpdatedAt = DateTimeOffset.UtcNow;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(cancellationToken);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> ApproveSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.Wajh)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        if (segment.Status != AudioSegmentStatus.Reviewed)
            throw new InvalidOperationException("Le segment doit être soumis à la revue avant approbation.");

        if (segment.StartMs < 0 || segment.EndMs <= segment.StartMs)
            throw new InvalidOperationException("Les limites temporelles du segment sont invalides.");

        if (segment.WajhId.HasValue && segment.Wajh != null && segment.Wajh.ReviewStatus != "Approved")
            throw new InvalidOperationException("Le وجه assigné doit être au statut 'Approved' dans le référentiel avant d'approuver le segment.");

        segment.Status = AudioSegmentStatus.Approved;
        segment.ApprovedByUserId = userId;
        segment.ApprovedAt = DateTimeOffset.UtcNow;
        segment.ValidatedByUserId = userId;
        segment.ValidatedAt = DateTimeOffset.UtcNow;
        segment.RejectionReason = null;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(cancellationToken);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> RejectSegmentAsync(Guid id, string reason, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Le motif de rejet est obligatoire.", nameof(reason));

        var segment = await _db.AudioSegments.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        segment.Status = AudioSegmentStatus.Rejected;
        segment.RejectionReason = reason.Trim();
        segment.ReviewedByUserId = userId;
        segment.ReviewedAt = DateTimeOffset.UtcNow;
        segment.ApprovedByUserId = null;
        segment.ApprovedAt = null;
        segment.ValidatedByUserId = null;
        segment.ValidatedAt = null;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(cancellationToken);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> RenderSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.SourceAudio)
            .Include(s => s.Ayah).ThenInclude(a => a.Surah)
            .Include(s => s.Wajh)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        if (segment.Status != AudioSegmentStatus.Approved && segment.Status != AudioSegmentStatus.Rendered)
            throw new InvalidOperationException("Valider et approuver le segment avant de générer son fichier audio.");

        segment.Status = AudioSegmentStatus.Rendering;
        segment.RowVersion = Guid.NewGuid().ToByteArray();
        await _db.SaveChangesAsync(cancellationToken);

        var sourcePhysical = _storage.GetPhysicalPath(segment.SourceAudio.StorageKey);
        if (!File.Exists(sourcePhysical))
            throw new FileNotFoundException("Fichier audio source introuvable sur le disque.", sourcePhysical);

        var surahNum = segment.Ayah?.Surah?.Number ?? 0;
        var ayahNum = segment.Ayah?.AyahNumber ?? 0;
        if (surahNum == 0 && !string.IsNullOrEmpty(segment.Reference) && segment.Reference.Contains(':'))
        {
            var parts = segment.Reference.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var a))
            {
                surahNum = s;
                ayahNum = a;
            }
        }
        var wajhSuffix = segment.Wajh != null ? $"_{segment.Wajh.Code}" : "";
        var rev = segment.Version > 0 ? segment.Version : 1;
        var relativeWorkingKey = $"working/segments/{surahNum:D3}/{ayahNum:D3}_{segment.Id:N}_rev{rev}{wajhSuffix}.mp3";
        var outputPhysical = _storage.GetPhysicalPath(relativeWorkingKey);

        var startTime = TimeSpan.FromMilliseconds(segment.StartMs);
        var duration = TimeSpan.FromMilliseconds(segment.EndMs - segment.StartMs);

        await _audioProcessor.ExtractSegmentAsync(sourcePhysical, outputPhysical, startTime, duration, cancellationToken);

        var probe = await _audioProcessor.ProbeAudioAsync(outputPhysical, cancellationToken);

        var audioAsset = new AudioAsset
        {
            OriginalFileName = Path.GetFileName(outputPhysical),
            StorageKey = relativeWorkingKey,
            DurationMs = (int)probe.Duration.TotalMilliseconds,
            SizeBytes = probe.SizeBytes,
            Bitrate = probe.Bitrate,
            MimeType = "audio/mpeg",
            Checksum = probe.Checksum,
            CreatedAt = DateTime.UtcNow
        };

        _db.AudioAssets.Add(audioAsset);
        await _db.SaveChangesAsync(cancellationToken);

        segment.PublishedAudioAssetId = audioAsset.Id;
        segment.Status = AudioSegmentStatus.Rendered;
        segment.RowVersion = Guid.NewGuid().ToByteArray();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Segment {Id} rev{Rev} rendu avec succès vers {Path}", id, rev, outputPhysical);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> PublishSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.SourceAudio)
            .Include(s => s.Ayah).ThenInclude(a => a.Surah)
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        if (segment.Status == AudioSegmentStatus.Published)
            return await GetSegmentDtoAsync(id, cancellationToken);

        if (segment.WajhId.HasValue && segment.Wajh?.ReviewStatus != "Approved")
            throw new InvalidOperationException("Le wajh doit être approuvé dans le référentiel avant publication.");

        if (segment.Status != AudioSegmentStatus.Rendered || segment.PublishedAudioAsset == null)
            throw new InvalidOperationException("Le segment doit être approuvé puis rendu avant publication.");

        var workingPhysical = _storage.GetPhysicalPath(segment.PublishedAudioAsset!.StorageKey);
        if (!File.Exists(workingPhysical))
            throw new FileNotFoundException("L'asset audio rendu est introuvable sur le disque.", workingPhysical);

        // Vérification de l'empreinte SHA-256
        var currentChecksum = _audioProcessor.ComputeChecksum(workingPhysical);
        if (!string.Equals(currentChecksum, segment.PublishedAudioAsset.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"L'empreinte du fichier rendu ({currentChecksum}) ne correspond pas à l'asset enregistré ({segment.PublishedAudioAsset.Checksum}).");
        }

        var surahNum = segment.Ayah?.Surah?.Number ?? 0;
        var ayahNum = segment.Ayah?.AyahNumber ?? 0;
        if (surahNum == 0 && !string.IsNullOrEmpty(segment.Reference) && segment.Reference.Contains(':'))
        {
            var parts = segment.Reference.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var a))
            {
                surahNum = s;
                ayahNum = a;
            }
        }
        var wajhSuffix = segment.Wajh != null ? $"_{segment.Wajh.Code}" : "";
        var rev = segment.Version > 0 ? segment.Version : 1;
        var publishedKey = $"published/{surahNum:D3}/{ayahNum:D3}/{segment.Id:N}_rev{rev}{wajhSuffix}.mp3";

        var publishedPhysical = _storage.GetPhysicalPath(publishedKey);
        var pubDir = Path.GetDirectoryName(publishedPhysical);
        if (!string.IsNullOrEmpty(pubDir)) Directory.CreateDirectory(pubDir);
        File.Copy(workingPhysical, publishedPhysical, overwrite: true);

        // Mettre à jour l'audio asset
        segment.PublishedAudioAsset.StorageKey = publishedKey;
        segment.PublishedAudioAsset.OriginalFileName = Path.GetFileName(publishedPhysical);

        // Recitation canonique si verset complet
        if (segment.SegmentType == AudioSegmentType.FullAyah
            && !segment.StartWordIndex.HasValue && !segment.EndWordIndex.HasValue
            && segment.AyahId.HasValue)
        {
            var recitation = await _db.Recitations
                .FirstOrDefaultAsync(r => r.AyahId == segment.AyahId.Value
                                       && r.ReciterId == segment.SourceAudio.ReciterId
                                       && r.RiwayaId == segment.SourceAudio.RiwayaId
                                       && r.WajhId == segment.WajhId, cancellationToken);

            if (recitation == null)
            {
                recitation = new Recitation
                {
                    AyahId = segment.AyahId.Value,
                    ReciterId = segment.SourceAudio.ReciterId,
                    RiwayaId = segment.SourceAudio.RiwayaId,
                    WajhId = segment.WajhId,
                    AudioAssetId = segment.PublishedAudioAsset.Id,
                    Status = RecitationStatus.Published,
                    IsPublished = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Recitations.Add(recitation);
            }
            else
            {
                recitation.AudioAssetId = segment.PublishedAudioAsset.Id;
                recitation.Status = RecitationStatus.Published;
                recitation.IsPublished = true;
                recitation.UpdatedAt = DateTime.UtcNow;
            }
        }

        segment.Status = AudioSegmentStatus.Published;
        segment.ApprovedByUserId = userId;
        segment.ApprovedAt = DateTimeOffset.UtcNow;
        segment.ValidatedByUserId = userId;
        segment.ValidatedAt = DateTimeOffset.UtcNow;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Segment {Id} rev{Rev} publié canoniquement vers {Path}", id, rev, publishedPhysical);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<AudioSegmentDto> UnpublishSegmentAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.SourceAudio)
            .Include(s => s.Ayah)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (segment == null)
            throw new KeyNotFoundException($"Segment {id} introuvable.");

        if (segment.Status != AudioSegmentStatus.Published)
            throw new InvalidOperationException("Seul un segment publié peut être retiré de la publication.");

        // Si relié à une récitation, la désactiver
        if (segment.AyahId.HasValue && segment.SourceAudio != null)
        {
            var recitation = await _db.Recitations
                .FirstOrDefaultAsync(r => r.AyahId == segment.AyahId
                                       && r.ReciterId == segment.SourceAudio.ReciterId
                                       && r.RiwayaId == segment.SourceAudio.RiwayaId
                                       && r.WajhId == segment.WajhId
                                       && r.AudioAssetId == segment.PublishedAudioAssetId, cancellationToken);
            if (recitation != null)
            {
                recitation.IsPublished = false;
                recitation.Status = RecitationStatus.Approved;
                recitation.UpdatedAt = DateTime.UtcNow;
            }
        }

        segment.Status = AudioSegmentStatus.Rendered;
        segment.UpdatedByUserId = userId;
        segment.UpdatedAt = DateTimeOffset.UtcNow;
        segment.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Publication du segment {Id} retirée par {User}", id, userId);

        return await GetSegmentDtoAsync(segment.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<AudioSegmentDto>> GetReviewQueueAsync(int? surahNumber = null, AudioSegmentStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AudioSegments
            .Include(s => s.Ayah).ThenInclude(a => a.Surah)
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .AsNoTracking();

        if (surahNumber.HasValue)
        {
            query = query.Where(s => s.SourceAudio != null && s.SourceAudio.Surah != null && s.SourceAudio.Surah.Number == surahNumber.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var list = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new AudioSegmentDto(
                s.Id,
                s.SourceAudioId,
                s.AyahId,
                s.Ayah != null ? (int?)s.Ayah.AyahNumber : null,
                s.WajhId,
                s.Wajh != null ? s.Wajh.Code : null,
                s.Wajh != null ? s.Wajh.NameArabic : null,
                s.StartMs,
                s.EndMs,
                s.StartWordIndex,
                s.EndWordIndex,
                s.SegmentType,
                s.SequenceNumber,
                s.Status,
                s.Notes,
                s.PublishedAudioAsset != null ? _storage.GetPublicUrl(s.PublishedAudioAsset.StorageKey) : null,
                Convert.ToBase64String(s.RowVersion),
                s.CreatedAt,
                s.RejectionReason,
                s.ReviewedByUserId,
                s.ReviewedAt,
                s.ApprovedByUserId,
                s.ApprovedAt,
                s.Version,
                s.Reference,
                s.Edition
            ))
            .ToListAsync(cancellationToken);

        return list;
    }

    public async Task<int> TokenizeAyahsAsync(int surahNumber, CancellationToken cancellationToken = default)
    {
        var ayahs = await _db.Ayahs
            .Include(a => a.Words)
            .Where(a => a.Surah.Number == surahNumber)
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (var ayah in ayahs)
        {
            if (!ayah.Words.Any())
            {
                TokenizeAyahInternal(ayah);
                count++;
            }
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    private void TokenizeAyahInternal(Ayah ayah)
    {
        if (string.IsNullOrWhiteSpace(ayah.TextWarsh)) return;

        var tokens = ayah.TextWarsh.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            var rawWord = tokens[i];
            var searchWord = NormalizeArabic(rawWord);

            var word = new AyahWord
            {
                AyahId = ayah.Id,
                WordIndex = i + 1,
                TextWarsh = rawWord,
                SearchText = searchWord
            };

            ayah.Words.Add(word);
            _db.AyahWords.Add(word);
        }
    }

    private static string NormalizeArabic(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var cleaned = ArabicDiacriticsRegex().Replace(text, "");
        cleaned = ArabicAlefRegex().Replace(cleaned, "ا");
        cleaned = cleaned.Replace("ى", "ي").Replace("ة", "ه");
        return cleaned.Trim();
    }

    private async Task<AudioSegmentDto> GetSegmentDtoAsync(Guid segmentId, CancellationToken cancellationToken)
    {
        var s = await _db.AudioSegments
            .Include(x => x.Ayah)
            .Include(x => x.Wajh)
            .Include(x => x.PublishedAudioAsset)
            .FirstAsync(x => x.Id == segmentId, cancellationToken);

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
            s.PublishedAudioAsset != null ? _storage.GetPublicUrl(s.PublishedAudioAsset.StorageKey) : null,
            Convert.ToBase64String(s.RowVersion),
            s.CreatedAt,
            s.RejectionReason,
            s.ReviewedByUserId,
            s.ReviewedAt,
            s.ApprovedByUserId,
            s.ApprovedAt,
            s.Version,
            s.Reference,
            s.Edition
        );
    }
}
