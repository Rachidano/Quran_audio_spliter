using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quran.Infrastructure.Persistence;
using Quran.Domain.Enums;

namespace Quran.Infrastructure.Services;

public static class AwjuhCatalog
{
    public static async Task InitializeAsync(QuranDbContext db)
    {
        // Use structured AwjuhService for versioned, audited seed that never recreates deleted rows.
        var service = new AwjuhService(db);
        await service.EnsureSeedDataAsync(force: false);
    }
    public static readonly string[] LegacyCodes = ["istiadha", "basmala", "w01", "w02", "w03", "taqlil", "silat_mim"];
    public static JsonElement Read()
    {
        using var stream = typeof(AwjuhCatalog).Assembly.GetManifestResourceStream("Quran.Infrastructure.Data.awjuh-catalog.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    public static async Task<int> ReplaceLegacyAsync(QuranDbContext db, CancellationToken cancellationToken = default)
    {
        // One SaveChanges transaction: detach assignments before deleting obsolete labels.
        var obsolete = await db.Awjuh.Where(w => LegacyCodes.Contains(w.Code)).ToListAsync(cancellationToken);
        var ids = obsolete.Select(w => w.Id).ToArray();
        foreach (var recitation in await db.Recitations.Where(r => r.WajhId != null && ids.Contains(r.WajhId.Value)).ToListAsync(cancellationToken))
        {
            recitation.WajhId = null; recitation.Wajh = null;
            recitation.Status = RecitationStatus.Imported; recitation.IsPublished = false;
        }
        foreach (var segment in await db.AudioSegments.Where(s => s.WajhId != null && ids.Contains(s.WajhId.Value)).ToListAsync(cancellationToken))
        {
            segment.WajhId = null; segment.Wajh = null; segment.Status = AudioSegmentStatus.Draft;
            segment.ValidatedAt = null; segment.ValidatedByUserId = null; segment.PublishedAudioAssetId = null;
        }
        db.Awjuh.RemoveRange(obsolete);
        await db.SaveChangesAsync(cancellationToken);
        return obsolete.Count;
    }
}
