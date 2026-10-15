using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed class AnnotationService
{
    private readonly WorkspaceService _workspaceService;
    private readonly Stack<UndoAction> _undoStack = new();

    public AnnotationService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    private string GetDbPath() => Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");

    private AppDbContext CreateDbContext() => new(GetDbPath());

    public async Task<List<AyahPassageEntity>> GetPassagesForSurahAsync(string audioSourceId, int surahNumber)
    {
        using var db = CreateDbContext();
        return await db.Passages
            .Where(p => p.AudioSourceId == audioSourceId && p.SurahNumber == surahNumber)
            .OrderBy(p => p.AyahNumber)
            .ThenBy(p => p.StartTimeSec)
            .ToListAsync();
    }

    public async Task<List<AyahPassageEntity>> GetPassagesForAyahAsync(string audioSourceId, string ayahId)
    {
        using var db = CreateDbContext();
        return await db.Passages
            .Where(p => p.AudioSourceId == audioSourceId && p.AyahId == ayahId)
            .OrderBy(p => p.StartTimeSec)
            .ToListAsync();
    }

    public async Task<(bool Success, string? ErrorMessage, AyahPassageEntity SavedPassage)> SavePassageAsync(
        AyahPassageEntity passage,
        double maxAudioDurationSec = 0)
    {
        // L06-08: Protections against zero duration, bounds out of range
        if (passage.StartTimeSec < 0)
        {
            return (false, "توقيت البداية لا يمكن أن يكون سالباً.", passage);
        }

        if (passage.EndTimeSec <= passage.StartTimeSec)
        {
            return (false, "توقيت النهاية يجب أن يكون أكبر من توقيت البداية.", passage);
        }

        if (maxAudioDurationSec > 0 && passage.EndTimeSec > maxAudioDurationSec + 0.5)
        {
            return (false, "توقيت النهاية يتجاوز المدة الإجمالية للتسجيل الصوتي.", passage);
        }

        using var db = CreateDbContext();

        // Verify audio source exists or resolve to existing source for this surah (FK safety)
        var audioExists = await db.AudioSources.AnyAsync(s => s.Id == passage.AudioSourceId);
        if (!audioExists)
            return (false, "نزّل تسجيل القارئ المحدد أو استورده قبل حفظ التوقيتات. لا يمكن استعمال تسجيل قارئ آخر.", passage);

        // Check if there is an existing passage to update or if we should insert
        if (passage.Id > 0)
        {
            var existing = await db.Passages.FindAsync(passage.Id);
            if (existing != null)
            {
                // Snapshot for undo
                var snapshot = ClonePassage(existing);
                _undoStack.Push(new UndoAction(UndoType.Update, snapshot));

                // L06-07: bounds modification increments revision to invalidate dependent compositions
                if (Math.Abs(existing.StartTimeSec - passage.StartTimeSec) > 0.01 ||
                    Math.Abs(existing.EndTimeSec - passage.EndTimeSec) > 0.01)
                {
                    existing.Revision++;
                }

                existing.StartTimeSec = passage.StartTimeSec;
                existing.EndTimeSec = passage.EndTimeSec;
                existing.Kind = passage.Kind;
                existing.IsSelectedForExport = passage.IsSelectedForExport;
                existing.IsValidatedByUser = passage.IsValidatedByUser;
                existing.Notes = passage.Notes;

                if (existing.IsSelectedForExport)
                {
                    var siblings = await db.Passages
                        .Where(p => p.AudioSourceId == existing.AudioSourceId && p.AyahId == existing.AyahId && p.Id != existing.Id)
                        .ToListAsync();
                    foreach (var s in siblings) s.IsSelectedForExport = false;
                }

                await db.SaveChangesAsync();
                return (true, null, existing);
            }
        }

        // Pour la prise principale (Full), incrémenter la révision de l'ayah si remplacement.
        // Pour les reprises (FullRetake, PartialRetake) et portions, chaque nouvelle prise est conservée distinctement (L06-05 & A04).
        if (passage.Id == 0 && passage.Kind == PassageKind.Full)
        {
            var existingMain = await db.Passages
                .FirstOrDefaultAsync(p => p.AudioSourceId == passage.AudioSourceId &&
                                          p.AyahId == passage.AyahId &&
                                          p.Kind == PassageKind.Full);

            if (existingMain != null)
            {
                // Snapshot for undo
                var snapshot = ClonePassage(existingMain);
                _undoStack.Push(new UndoAction(UndoType.Update, snapshot));

                existingMain.Revision++;
                existingMain.StartTimeSec = passage.StartTimeSec;
                existingMain.EndTimeSec = passage.EndTimeSec;
                existingMain.IsSelectedForExport = passage.IsSelectedForExport;
                existingMain.IsValidatedByUser = passage.IsValidatedByUser;
                existingMain.Notes = passage.Notes;

                if (existingMain.IsSelectedForExport)
                {
                    var siblings = await db.Passages
                        .Where(p => p.AudioSourceId == existingMain.AudioSourceId && p.AyahId == existingMain.AyahId && p.Id != existingMain.Id)
                        .ToListAsync();
                    foreach (var s in siblings) s.IsSelectedForExport = false;
                }

                await db.SaveChangesAsync();
                return (true, null, existingMain);
            }
        }

        passage.Revision = 1;
        passage.CreatedAtUtc = DateTime.UtcNow;
        db.Passages.Add(passage);

        if (passage.IsSelectedForExport)
        {
            var siblings = await db.Passages
                .Where(p => p.AudioSourceId == passage.AudioSourceId && p.AyahId == passage.AyahId)
                .ToListAsync();
            foreach (var s in siblings) s.IsSelectedForExport = false;
        }

        await db.SaveChangesAsync();

        _undoStack.Push(new UndoAction(UndoType.Insert, ClonePassage(passage)));
        return (true, null, passage);
    }

    public async Task<AyahPassageEntity?> UndoLastSaveAsync()
    {
        if (_undoStack.Count == 0) return null;

        var lastAction = _undoStack.Pop();
        using var db = CreateDbContext();

        if (lastAction.Type == UndoType.Insert)
        {
            var entity = await db.Passages.FindAsync(lastAction.Passage.Id);
            if (entity != null)
            {
                db.Passages.Remove(entity);
                await db.SaveChangesAsync();
            }
            return lastAction.Passage;
        }
        else if (lastAction.Type == UndoType.Update)
        {
            var entity = await db.Passages.FindAsync(lastAction.Passage.Id);
            if (entity != null)
            {
                entity.StartTimeSec = lastAction.Passage.StartTimeSec;
                entity.EndTimeSec = lastAction.Passage.EndTimeSec;
                entity.Kind = lastAction.Passage.Kind;
                entity.IsSelectedForExport = lastAction.Passage.IsSelectedForExport;
                entity.Revision = lastAction.Passage.Revision;
                entity.IsValidatedByUser = lastAction.Passage.IsValidatedByUser;
                entity.Notes = lastAction.Passage.Notes;
                await db.SaveChangesAsync();
            }
            return lastAction.Passage;
        }

        return null;
    }

    public bool CanUndo => _undoStack.Count > 0;

    public async Task<bool> ValidatePassageAsync(int passageId, bool isValidated)
    {
        using var db = CreateDbContext();
        var entity = await db.Passages.FindAsync(passageId);
        if (entity == null) return false;

        entity.IsValidatedByUser = isValidated;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SelectPassageForExportAsync(int passageId)
    {
        using var db = CreateDbContext();
        var entity = await db.Passages.FindAsync(passageId);
        if (entity == null) return false;

        var siblings = await db.Passages
            .Where(p => p.AudioSourceId == entity.AudioSourceId && p.AyahId == entity.AyahId)
            .ToListAsync();

        foreach (var s in siblings)
        {
            s.IsSelectedForExport = (s.Id == passageId);
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePassageAsync(int passageId)
    {
        using var db = CreateDbContext();
        var entity = await db.Passages.FindAsync(passageId);
        if (entity == null) return false;

        db.Passages.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    // L06-09: Export annotations with format version, edition, audio SHA-256
    public async Task<string> ExportAnnotationsToJsonAsync(
        string audioSourceId,
        int surahNumber,
        string outputPath,
        string editionCode = "newMushaf")
    {
        using var db = CreateDbContext();
        var audioSource = await db.AudioSources.FindAsync(audioSourceId);
        var passages = await db.Passages
            .Where(p => p.AudioSourceId == audioSourceId && p.SurahNumber == surahNumber)
            .OrderBy(p => p.AyahNumber)
            .ThenBy(p => p.StartTimeSec)
            .ToListAsync();

        var exportData = new AnnotationExportModel
        {
            FormatVersion = "1.0",
            EditionCode = editionCode,
            SurahNumber = surahNumber,
            AudioSourceId = audioSourceId,
            AudioSha256 = audioSource?.Sha256 ?? string.Empty,
            ExportedAtUtc = DateTime.UtcNow,
            Passages = passages.Select(p => new PassageExportItem
            {
                Id = p.Id,
                SurahNumber = p.SurahNumber,
                AyahNumber = p.AyahNumber,
                AyahId = p.AyahId,
                StartTimeSec = p.StartTimeSec,
                EndTimeSec = p.EndTimeSec,
                Kind = p.Kind.ToString(),
                IsSelectedForExport = p.IsSelectedForExport,
                Revision = p.Revision,
                IsValidatedByUser = p.IsValidatedByUser,
                Notes = p.Notes
            }).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(exportData, options);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(outputPath, json);
        return outputPath;
    }

    // L06-09: Import annotations with schema revision and audio SHA-256 matching
    public async Task<(bool Success, string ErrorMessage, int ImportedCount)> ImportAnnotationsFromJsonAsync(
        string jsonFilePath,
        string expectedAudioSha256,
        string expectedAudioSourceId,
        int expectedSurahNumber,
        string expectedEditionCode = "newMushaf")
    {
        if (!File.Exists(jsonFilePath))
        {
            return (false, "ملف التضمينات والتوقيتات غير موجود.", 0);
        }

        var json = await File.ReadAllTextAsync(jsonFilePath);
        AnnotationExportModel? model;
        try
        {
            model = JsonSerializer.Deserialize<AnnotationExportModel>(json);
        }
        catch (Exception ex)
        {
            return (false, $"ملف التوقيتات تالف أو غير صالح كـ JSON: {ex.Message}", 0);
        }

        if (model == null)
        {
            return (false, "تعذر قراءة بيانات التوقيتات.", 0);
        }

        // L06-09: Reject if formatVersion incompatible
        if (model.FormatVersion != "1.0")
        {
            return (false, $"إصدار التنسيق غير مدعوم ({model.FormatVersion}). الإصدار المطلوب: 1.0", 0);
        }

        // L06-09: Reject if edition mismatch
        if (!string.Equals(model.EditionCode, expectedEditionCode, StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"طبعة المصحف غير متوافقة ({model.EditionCode} مقابل {expectedEditionCode}).", 0);
        }

        // L06-09: Reject if audio SHA-256 does not match!
        if (!string.IsNullOrEmpty(expectedAudioSha256))
        {
            if (string.IsNullOrEmpty(model.AudioSha256) ||
                !string.Equals(model.AudioSha256, expectedAudioSha256, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "الملف غير متطابق: البصمة الصوتية (SHA-256) للتسجيل لا تطابق الملف الحالي أو أنها مفقودة في ملف الاستيراد.", 0);
            }
        }

        if (model.SurahNumber != expectedSurahNumber)
        {
            return (false, $"رقم السورة في الملف ({model.SurahNumber}) لا يطابق السورة الحالية ({expectedSurahNumber}).", 0);
        }

        using var db = CreateDbContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Retrieve existing passages to update in place where possible, preserving foreign keys in projects/compositions
        var existing = await db.Passages
            .Where(p => p.AudioSourceId == expectedAudioSourceId && p.SurahNumber == expectedSurahNumber)
            .ToListAsync();
        var existingQueue = existing.GroupBy(p => p.AyahId).ToDictionary(g => g.Key, g => new Queue<AyahPassageEntity>(g));

        int importedCount = 0;
        var usedExistingIds = new HashSet<int>();

        foreach (var item in model.Passages)
        {
            var kind = Enum.TryParse<PassageKind>(item.Kind, out var parsedKind) ? parsedKind : PassageKind.Full;

            if (existingQueue.TryGetValue(item.AyahId, out var queue) && queue.Count > 0)
            {
                var existingEntity = queue.Dequeue();
                usedExistingIds.Add(existingEntity.Id);
                existingEntity.StartTimeSec = item.StartTimeSec;
                existingEntity.EndTimeSec = item.EndTimeSec;
                existingEntity.Kind = kind;
                existingEntity.IsSelectedForExport = item.IsSelectedForExport;
                existingEntity.Revision = item.Revision > 0 ? item.Revision : existingEntity.Revision + 1;
                existingEntity.IsValidatedByUser = item.IsValidatedByUser;
                existingEntity.Notes = item.Notes;
            }
            else
            {
                var entity = new AyahPassageEntity
                {
                    AudioSourceId = expectedAudioSourceId,
                    SurahNumber = item.SurahNumber,
                    AyahNumber = item.AyahNumber,
                    AyahId = item.AyahId,
                    StartTimeSec = item.StartTimeSec,
                    EndTimeSec = item.EndTimeSec,
                    Kind = kind,
                    IsSelectedForExport = item.IsSelectedForExport,
                    Revision = item.Revision,
                    IsValidatedByUser = item.IsValidatedByUser,
                    Notes = item.Notes,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.Passages.Add(entity);
            }
            importedCount++;
        }

        // Only remove obsolete unused passages that are not referenced
        var obsolete = existing.Where(e => !usedExistingIds.Contains(e.Id)).ToList();
        if (obsolete.Count > 0)
        {
            db.Passages.RemoveRange(obsolete);
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return (true, string.Empty, importedCount);
    }

    private static AyahPassageEntity ClonePassage(AyahPassageEntity p) => new()
    {
        Id = p.Id,
        AudioSourceId = p.AudioSourceId,
        SurahNumber = p.SurahNumber,
        AyahNumber = p.AyahNumber,
        AyahId = p.AyahId,
        StartTimeSec = p.StartTimeSec,
        EndTimeSec = p.EndTimeSec,
        Kind = p.Kind,
        IsSelectedForExport = p.IsSelectedForExport,
        Revision = p.Revision,
        IsValidatedByUser = p.IsValidatedByUser,
        Notes = p.Notes,
        CreatedAtUtc = p.CreatedAtUtc
    };

    private record UndoAction(UndoType Type, AyahPassageEntity Passage);
    private enum UndoType { Insert, Update }
}

public sealed class AnnotationExportModel
{
    [JsonPropertyName("formatVersion")]
    public string FormatVersion { get; set; } = "1.0";

    [JsonPropertyName("editionCode")]
    public string EditionCode { get; set; } = "newMushaf";

    [JsonPropertyName("surahNumber")]
    public int SurahNumber { get; set; }

    [JsonPropertyName("audioSourceId")]
    public string AudioSourceId { get; set; } = string.Empty;

    [JsonPropertyName("audioSha256")]
    public string AudioSha256 { get; set; } = string.Empty;

    [JsonPropertyName("exportedAtUtc")]
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("passages")]
    public List<PassageExportItem> Passages { get; set; } = new();
}

public sealed class PassageExportItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("surahNumber")]
    public int SurahNumber { get; set; }

    [JsonPropertyName("ayahNumber")]
    public int AyahNumber { get; set; }

    [JsonPropertyName("ayahId")]
    public string AyahId { get; set; } = string.Empty;

    [JsonPropertyName("startTimeSec")]
    public double StartTimeSec { get; set; }

    [JsonPropertyName("endTimeSec")]
    public double EndTimeSec { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Full";

    [JsonPropertyName("isSelectedForExport")]
    public bool IsSelectedForExport { get; set; }

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("isValidatedByUser")]
    public bool IsValidatedByUser { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
