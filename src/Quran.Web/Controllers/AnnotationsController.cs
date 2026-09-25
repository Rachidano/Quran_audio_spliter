using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;
using Quran.Infrastructure.Services;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/annotations")]
public class AnnotationsController(QuranDbContext db, IStorageService storage, IAudioProcessorService processor, MarwPilotCorpus corpus) : ControllerBase
{
    public record SourceInput(string StorageKey, string Sha256);
    public record ImportInput(string Edition, SourceInput Source, JsonElement[] Passages);
    public record ReviewInput(int Version, bool ReferenceChecked);
    private static JsonNode Clean(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        foreach (var key in new[] { "serverVersion", "status", "audioUrl", "wajhName", "sequenceNumber", "wajhStatus", "wajhId", "referenceStatus" }) node.Remove(key);
        foreach (var key in node.Where(p => p.Value == null).Select(p => p.Key).ToArray()) node.Remove(key);
        return node;
    }
    private static JsonObject View(MushafPassage p)
    {
        var node = JsonNode.Parse(p.ObservationJson) as JsonObject ?? new JsonObject();
        node["id"] = p.Id.ToString(); node["startMs"] = p.StartMs; node["endMs"] = p.EndMs; node["reference"] = p.Reference;
        node["segmentType"] = p.SegmentType; node["repeatOfId"] = p.RepeatOfId?.ToString(); node["notes"] = p.Notes;
        node["serverVersion"] = p.Version; node["status"] = p.Status; node["wajhCatalogCode"] = p.Wajh?.Code;
        node["wajhName"] = p.Wajh?.NameArabic; node["wajhStatus"] = p.Wajh?.ReviewStatus ?? "Unqualified";
        node["audioUrl"] = "/audio/" + p.SourceKey; return node;
    }
    [HttpGet]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<IActionResult> List([FromQuery] string sourceHash)
        => Ok((await db.MushafPassages.Include(p => p.Wajh).Where(p => p.SourceHash == sourceHash).OrderBy(p => p.StartMs).ToListAsync()).Select(View));

    [HttpPost("import")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<IActionResult> Import(ImportInput input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (input.Edition != MarwPilotCorpus.EditionCode || input.Source == null || input.Passages == null || input.Passages.Length > 10000 || !System.Text.RegularExpressions.Regex.IsMatch(input.Source.StorageKey ?? "", @"^sources/[0-9]{3}/original\.mp3$")) return BadRequest(new { message = "نسخة المصحف أو المصدر غير صالح." });
        var path = storage.GetPhysicalPath(input.Source.StorageKey);
        if (!System.IO.File.Exists(path)) return BadRequest(new { message = "المصدر الصوتي غير موجود." });
        await using (var stream = System.IO.File.OpenRead(path))
            if (!Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).Equals(input.Source.Sha256, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "بصمة الصوت لا تطابق الملف." });
        var duration = (long)(await processor.ProbeAudioAsync(path, ct)).Duration.TotalMilliseconds;
        var surah = int.Parse(input.Source.StorageKey.Split('/')[1]);
        var refs = corpus.PageNumbers.SelectMany(n => corpus.GetPage(n)!.Regions).Where(r => r.SurahNumber == surah).Select(r => r.Reference).ToHashSet();
        var ids = new HashSet<Guid>();
        var saved = new List<MushafPassage>();
        try
        {
            foreach (var raw in input.Passages)
            {
                var id = raw.GetProperty("id").GetGuid(); if (id == Guid.Empty || !ids.Add(id)) throw new ArgumentException("معرف مقطع مكرر أو غير صالح.");
                var start = raw.GetProperty("startMs").GetInt64(); var end = raw.GetProperty("endMs").GetInt64();
                string? reference = raw.TryGetProperty("reference", out var re) && re.ValueKind == JsonValueKind.String ? re.GetString() : null;
                var type = raw.GetProperty("segmentType").GetInt32();
                if (start < 0 || end - start < 200 || end > duration || !new[] { 1, 2, 3, 4, 99 }.Contains(type) || (reference != null && !refs.Contains(reference))) throw new ArgumentException("حدود المقطع أو الآية غير صالحة.");
                var repeat = raw.TryGetProperty("repeatOfId", out var rp) && rp.ValueKind == JsonValueKind.String ? rp.GetGuid() : (Guid?)null;
                var code = raw.TryGetProperty("wajhCatalogCode", out var wc) && wc.ValueKind == JsonValueKind.String ? wc.GetString() : null;
                var wajh = code == null ? null : await db.Awjuh.FirstOrDefaultAsync(w => w.Code == code && w.ReviewStatus != "Archived", ct);
                if (code != null && wajh == null) throw new ArgumentException("الوجه غير موجود أو مؤرشف.");
                if (wajh != null)
                {
                    using var definition = JsonDocument.Parse(wajh.DefinitionJson);
                    if (definition.RootElement.TryGetProperty("application", out var app) && app.TryGetProperty("sourceSurah", out var ss) && ss.GetInt32() != surah) throw new ArgumentException("الوجه خاص بسورة أخرى.");
                }
                var old = await db.MushafPassages.FindAsync([id], ct);
                if (old != null && (old.SourceHash != input.Source.Sha256 || old.SourceKey != input.Source.StorageKey || old.Edition != input.Edition)) throw new ArgumentException("المعرف مرتبط بمصدر آخر.");
                if (old != null && (!raw.TryGetProperty("serverVersion", out var v) || v.GetInt32() != old.Version)) return Conflict(new { message = "تغيرت المقاطع في النظام. استرجعها قبل حفظ التعديلات." });
                if (old != null && JsonNode.DeepEquals(Clean(old.ObservationJson), Clean(raw.GetRawText())))
                {
                    old.Wajh = wajh; saved.Add(old); continue;
                }
                var p = old ?? new MushafPassage { Id = id, Edition = input.Edition, SourceKey = input.Source.StorageKey, SourceHash = input.Source.Sha256 };
                p.StartMs = start; p.EndMs = end; p.Reference = reference; p.SegmentType = type; p.RepeatOfId = repeat; p.Wajh = wajh; p.WajhId = wajh?.Id;
                p.Notes = raw.TryGetProperty("notes", out var note) ? note.GetString() ?? "" : "";
                if (p.Notes.Length > 4000 || raw.GetRawText().Length > 16000) throw new ArgumentException("الملاحظات طويلة جدًا.");
                p.ObservationJson = raw.GetRawText(); p.Status = "Draft"; p.UpdatedAt = DateTimeOffset.UtcNow;
                if (old == null) db.MushafPassages.Add(p); else p.Version++;
                saved.Add(p);
            }
            foreach (var p in saved.Where(p => p.RepeatOfId != null))
            {
                var target = saved.FirstOrDefault(t => t.Id == p.RepeatOfId) ?? await db.MushafPassages.FindAsync([p.RepeatOfId!.Value], ct);
                if (p.SegmentType != 3 || target == null || target.Id == p.Id || target.StartMs >= p.StartMs || target.SourceHash != p.SourceHash || target.Edition != p.Edition || target.Reference != p.Reference) throw new ArgumentException("رابط الإعادة غير صالح: اختر مقطعًا سابقًا من الآية والمصدر نفسيهما.");
            }
            // Also protect existing children if their parent was moved or reassigned.
            foreach (var child in await db.MushafPassages.Where(p => p.RepeatOfId != null && ids.Contains(p.RepeatOfId.Value)).ToListAsync(ct))
            {
                var parent = saved.First(p => p.Id == child.RepeatOfId);
                if (parent.StartMs >= child.StartMs || parent.Reference != child.Reference) throw new ArgumentException("التعديل لا يوافق المقاطع المعادة المرتبطة به.");
            }

            // Sync with canonical AudioSegments
            var sourceAudio = await db.SourceAudios.FirstOrDefaultAsync(s => s.StorageKey == input.Source.StorageKey, ct);
            if (sourceAudio == null) throw new ArgumentException("سجل المصدر الصوتي قبل استيراد المقاطع.");
            if (sourceAudio != null)
            {
                foreach (var p in saved)
                {
                    var seg = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == p.Id, ct);
                    if (seg == null)
                    {
                        seg = new AudioSegment
                        {
                            Id = p.Id,
                            SourceAudioId = sourceAudio.Id,
                            StartMs = p.StartMs,
                            EndMs = p.EndMs,
                            Reference = p.Reference,
                            SegmentType = (Quran.Domain.Enums.AudioSegmentType)p.SegmentType,
                            RepeatOfId = p.RepeatOfId,
                            WajhId = p.WajhId,
                            Notes = p.Notes,
                            Edition = p.Edition,
                            SourceHash = p.SourceHash,
                            ObservationJson = p.ObservationJson,
                            Status = Quran.Domain.Enums.AudioSegmentStatus.Draft,
                            Version = p.Version,
                            CreatedByUserId = User.Identity?.Name ?? "Reviewer",
                            CreatedAt = p.UpdatedAt,
                            RowVersion = Guid.NewGuid().ToByteArray()
                        };
                        db.AudioSegments.Add(seg);
                        db.SegmentRevisions.Add(new SegmentRevision
                        {
                            AudioSegmentId = seg.Id,
                            RevisionNumber = 1,
                            StartMs = seg.StartMs,
                            EndMs = seg.EndMs,
                            Reference = seg.Reference,
                            SegmentType = seg.SegmentType,
                            WajhId = seg.WajhId,
                            ProofNotes = seg.Notes,
                            CreatedByUserId = User.Identity?.Name ?? "Reviewer",
                            CreatedAt = p.UpdatedAt
                        });
                    }
                    else
                    {
                        if (seg.SourceHash != p.SourceHash || seg.SourceAudioId != sourceAudio.Id)
                            throw new ArgumentException("Le segment canonique appartient à une autre source.");
                        // Never overwrite changes made in the canonical studio via a stale legacy export.
                        if (seg.Version > p.Version)
                            throw new DbUpdateConcurrencyException("L'export est plus ancien que la version du studio.");
                        bool changed = seg.StartMs != p.StartMs || seg.EndMs != p.EndMs || seg.Reference != p.Reference
                            || seg.WajhId != p.WajhId || (int)seg.SegmentType != p.SegmentType || seg.RepeatOfId != p.RepeatOfId || seg.Notes != p.Notes;
                        if (!changed) continue;
                        seg.StartMs = p.StartMs;
                        seg.EndMs = p.EndMs;
                        seg.Reference = p.Reference;
                        seg.SegmentType = (Quran.Domain.Enums.AudioSegmentType)p.SegmentType;
                        seg.RepeatOfId = p.RepeatOfId;
                        seg.WajhId = p.WajhId;
                        seg.Notes = p.Notes;
                        seg.ObservationJson = p.ObservationJson;
                        seg.Version = p.Version;
                        seg.UpdatedAt = DateTimeOffset.UtcNow;
                        seg.UpdatedByUserId = User.Identity?.Name ?? "Reviewer";
                        seg.Status = Quran.Domain.Enums.AudioSegmentStatus.Draft;
                        seg.PublishedAudioAssetId = null;
                        seg.ApprovedAt = null; seg.ApprovedByUserId = null;
                        seg.ReviewedAt = null; seg.ReviewedByUserId = null;
                        seg.ValidatedAt = null; seg.ValidatedByUserId = null;
                        seg.RowVersion = Guid.NewGuid().ToByteArray();
                        var revision = (await db.SegmentRevisions.Where(r => r.AudioSegmentId == seg.Id).MaxAsync(r => (int?)r.RevisionNumber, ct) ?? 0) + 1;
                        db.SegmentRevisions.Add(new SegmentRevision { AudioSegmentId = seg.Id, RevisionNumber = revision,
                            StartMs = seg.StartMs, EndMs = seg.EndMs, Reference = seg.Reference, SegmentType = seg.SegmentType,
                            WajhId = seg.WajhId, ProofNotes = seg.Notes, CreatedByUserId = seg.UpdatedByUserId, CreatedAt = DateTimeOffset.UtcNow });
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException) { return BadRequest(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "تعارض حفظ. استرجع المقاطع مجددًا." }); }
        return Ok(saved.OrderBy(p => p.StartMs).Select(View));
    }
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public IActionResult Publish(Guid id, ReviewInput input)
        => Conflict(new { message = "انشر من قائمة المراجعة بعد الاعتماد وإنتاج الملف الصوتي؛ النشر المباشر القديم متوقف." });
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int version)
    {
        var p = await db.MushafPassages.FindAsync(id); if (p == null) return NotFound();
        if (p.Status == "Published") return Conflict(new { message = "اسحب النشر قبل الحذف." });
        if (p.Version != version) return Conflict(new { message = "أعد تحميل المقاطع." });
        var canonical = await db.AudioSegments.FindAsync(id);
        if (canonical != null && (canonical.Status != Quran.Domain.Enums.AudioSegmentStatus.Draft || canonical.Version != version))
            return Conflict(new { message = "عدّل المقطع من الاستوديو؛ نسخته أو حالته تغيرت." });
        if (await db.MushafPassages.AnyAsync(c => c.RepeatOfId == id)) return Conflict(new { message = "احذف أو عدّل روابط الإعادة أولًا." });
        db.Remove(p);
        var seg = await db.AudioSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (seg != null) db.AudioSegments.Remove(seg);
        await db.SaveChangesAsync(); return NoContent();
    }
    [HttpGet("playback")]
    [AllowAnonymous]
    public async Task<IActionResult> Playback([FromQuery] string reference)
    {
        var segments = await db.AudioSegments
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .Where(s => s.Edition == MarwPilotCorpus.EditionCode && s.PublishedAudioAssetId != null && s.Reference == reference && s.Status == Quran.Domain.Enums.AudioSegmentStatus.Published && (s.Wajh == null || s.Wajh.ReviewStatus == "Approved"))
            .OrderBy(s => s.StartMs)
            .ToListAsync();

        if (segments.Count > 0)
        {
            var results = segments.Select(s => new
            {
                id = s.Id.ToString(),
                startMs = s.StartMs,
                endMs = s.EndMs,
                reference = s.Reference,
                segmentType = (int)s.SegmentType,
                repeatOfId = s.RepeatOfId?.ToString(),
                notes = s.Notes,
                serverVersion = s.Version,
                status = s.Status.ToString(),
                wajhCatalogCode = s.Wajh?.Code,
                wajhName = s.Wajh?.NameArabic,
                wajhStatus = s.Wajh?.ReviewStatus ?? "Unqualified",
                audioUrl = s.PublishedAudioAsset != null ? "/audio/" + s.PublishedAudioAsset.StorageKey : null,
                isCutAsset = s.PublishedAudioAsset != null,
                downloadUrl = s.PublishedAudioAsset != null ? "/audio/" + s.PublishedAudioAsset.StorageKey : null
            });
            return Ok(results);
        }

        return Ok(Array.Empty<object>());
    }

    [HttpGet("preview")]
    [Authorize(Roles = "Reviewer,Admin")]
    public async Task<IActionResult> Preview([FromQuery] string reference)
    {
        var segments = await db.AudioSegments
            .Include(s => s.Wajh)
            .Include(s => s.PublishedAudioAsset)
            .Include(s => s.SourceAudio)
            .Where(s => s.Reference == reference)
            .OrderBy(s => s.StartMs)
            .ToListAsync();

        if (segments.Count > 0)
        {
            var results = segments.Select(s => new
            {
                id = s.Id.ToString(),
                startMs = s.StartMs,
                endMs = s.EndMs,
                reference = s.Reference,
                segmentType = (int)s.SegmentType,
                repeatOfId = s.RepeatOfId?.ToString(),
                notes = s.Notes,
                serverVersion = s.Version,
                status = s.Status.ToString(),
                wajhCatalogCode = s.Wajh?.Code,
                wajhName = s.Wajh?.NameArabic,
                wajhStatus = s.Wajh?.ReviewStatus ?? "Unqualified",
                audioUrl = s.PublishedAudioAsset != null ? "/audio/" + s.PublishedAudioAsset.StorageKey : (s.SourceAudio != null ? "/audio/" + s.SourceAudio.StorageKey : null),
                isCutAsset = s.PublishedAudioAsset != null,
                downloadUrl = s.PublishedAudioAsset != null ? "/audio/" + s.PublishedAudioAsset.StorageKey : null
            });
            return Ok(results);
        }

        var query = db.MushafPassages.Include(p => p.Wajh)
            .Where(p => p.Edition == MarwPilotCorpus.EditionCode && p.Reference == reference);
        return Ok((await query.OrderBy(p => p.StartMs).ToListAsync()).Select(View));
    }
}
