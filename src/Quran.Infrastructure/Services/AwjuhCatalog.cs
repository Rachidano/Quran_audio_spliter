using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quran.Infrastructure.Persistence;
using Quran.Domain.Enums;

namespace Quran.Infrastructure.Services;

public static class AwjuhCatalog
{
    public static async Task InitializeAsync(QuranDbContext db)
    {
        // Explicit one-time bootstrap only: normal startup never recreates deleted catalog rows.
        foreach (var entry in Read().GetProperty("entries").EnumerateArray())
        {
            var code = entry.GetProperty("code").GetString()!;
            if (await db.Awjuh.AnyAsync(w => w.Code == code)) continue;
            db.Awjuh.Add(new Quran.Domain.Entities.Wajh {
                Code = code, NameArabic = entry.GetProperty("nameArabic").GetString()!,
                RouteCode = entry.GetProperty("routeCode").GetString()!,
                Description = entry.GetProperty("scope").GetString(),
                SourceCitation = string.Join("؛ ", entry.GetProperty("sourceIds").EnumerateArray().Select(id => {
                    var source = Read().GetProperty("sources").EnumerateArray().First(s => s.GetProperty("id").GetString() == id.GetString());
                    return source.GetProperty("title").GetString() + " — " + source.GetProperty("locator").GetString();
                })),
                DefinitionJson = entry.GetRawText(), ReviewStatus = "PendingReview"
            });
        }
        await db.SaveChangesAsync();
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
