using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed record RecitationChoice(string CollectionId, string ReciterId, string Name);

// The embedded inventory is only a seed. User edits live in SQLite and are never
// overwritten at startup. No JSON file is required in the user workflow.
public sealed class RecitationCatalogService(WorkspaceService workspace)
{
    public const string SaberCollection = "saber_abdelhakam_warsh";
    public const string HusaryCollection = "husary_murattal_warsh";
    /// <summary>mp3quran.net : minutages ayah par ayah de الحصري، رواية ورش (dossier audio r3, compte Warsh).</summary>
    public const int HusaryTimingReadId = 120;
    /// <summary>Fichiers MP3 de الحصري ورش sur mp3quran.net (moshaf 120, « ورش عن نافع - مرتل »).</summary>
    public const string HusaryUrlTemplate = "https://cdn.mp3quran.net/audio/mahmoud-husary/r3/{0:D3}.mp3";
    /// <summary>Anciens liens par défaut (quranicaudio.com), retirés par le site : ils renvoient 404.</summary>
    public const string LegacyHusaryUrlPrefix = "https://download.quranicaudio.com/quran/warsh/mahmood_khaleel_al-husaree_warsh/";
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);
    private AppDbContext Db() => new(workspace.GetDatabasePath());

    public async Task InitializeAsync()
    {
        await InitializationGate.WaitAsync();
        try
        {
            using var db = Db();
            await db.Database.EnsureCreatedAsync();
            // Additive schema upgrade for already-installed workspaces.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS audio_download_links (
                    CollectionId TEXT NOT NULL REFERENCES recitation_collections(Id),
                    SurahNumber INTEGER NOT NULL CHECK(SurahNumber BETWEEN 1 AND 114),
                    Url TEXT NOT NULL DEFAULT '', PRIMARY KEY(CollectionId, SurahNumber));
                CREATE TABLE IF NOT EXISTS audio_catalog_preferences (
                    Id INTEGER PRIMARY KEY CHECK(Id = 1), DefaultCollectionId TEXT NOT NULL);
                """);
            var hasTimingColumn = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('recitation_collections') WHERE name = 'TimingReadId'")
                .SingleAsync();
            if (hasTimingColumn == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE recitation_collections ADD COLUMN TimingReadId INTEGER");
            using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE reciters SET NameArabic = 'الشيخ صابر عبد الحكم' WHERE Id = 'saber_abdelhakam' AND NameArabic = 'الشيخ صابر الحكم';
                UPDATE recitation_collections SET TitleArabic = 'الشيخ صابر عبد الحكم' WHERE Id = 'saber_abdelhakam_warsh' AND TitleArabic = 'الشيخ صابر الحكم';
                """);
            if (!await db.Reciters.AnyAsync(r => r.Id == "saber_abdelhakam"))
                db.Reciters.Add(new ReciterEntity { Id = "saber_abdelhakam", NameArabic = "الشيخ صابر عبد الحكم", NameLatin = "Saber Abdelhakam" });
            if (!await db.RecitationCollections.AnyAsync(c => c.Id == SaberCollection))
                db.RecitationCollections.Add(new RecitationCollectionEntity { Id = SaberCollection, ReciterId = "saber_abdelhakam", TitleArabic = "الشيخ صابر عبد الحكم", Riwaya = "warsh" });

            // Récitations Warsh de référence (mp3quran.net) : ajoutées si absentes, sans écraser un nom renommé
            foreach (var reference in WarshRecitationReferential.All)
            {
                if (!await db.Reciters.AnyAsync(r => r.Id == reference.ReciterId))
                    db.Reciters.Add(new ReciterEntity { Id = reference.ReciterId, NameArabic = reference.NameArabic, NameLatin = reference.NameLatin });
                if (!await db.RecitationCollections.AnyAsync(c => c.Id == reference.CollectionId))
                    db.RecitationCollections.Add(new RecitationCollectionEntity
                    {
                        Id = reference.CollectionId,
                        ReciterId = reference.ReciterId,
                        TitleArabic = reference.TitleArabic,
                        Riwaya = "warsh",
                        BaseUrlTemplate = reference.UrlTemplate,
                        TimingReadId = reference.TimingReadId
                    });
            }

            await db.SaveChangesAsync();

            // Noms de récitateurs contenant la riwaya (ancienne version, ou titre recopié à l'enregistrement) :
            // nom de référence pour le référentiel, sinon nom nettoyé
            var referenceNames = WarshRecitationReferential.All.ToDictionary(r => r.ReciterId, r => r.NameArabic);
            foreach (var reciter in await db.Reciters.ToListAsync())
            {
                var cleaned = CleanReciterName(reciter.NameArabic);
                if (cleaned == reciter.NameArabic.Trim()) continue;
                reciter.NameArabic = referenceNames.GetValueOrDefault(reciter.Id) ?? cleaned;
            }
            foreach (var reference in WarshRecitationReferential.All)
            {
                // Ancien titre par défaut « المصحف المرتل برواية ورش … » : titre de référence
                var legacyTitle = "المصحف المرتل%";
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE recitation_collections SET TitleArabic = {reference.TitleArabic} WHERE Id = {reference.CollectionId} AND TitleArabic LIKE {legacyTitle}");
            }
            await db.SaveChangesAsync();

            // Les espaces créés par une ancienne version pointent vers quranicaudio.com (404) pour الحصري
            var legacyPattern = LegacyHusaryUrlPrefix + "%";
            foreach (var reference in WarshRecitationReferential.All)
            {
                if (reference.TimingReadId is int readId)
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE recitation_collections SET TimingReadId = {readId} WHERE Id = {reference.CollectionId} AND TimingReadId IS NULL");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE recitation_collections SET BaseUrlTemplate = {reference.UrlTemplate} WHERE Id = {reference.CollectionId} AND (BaseUrlTemplate = '' OR BaseUrlTemplate LIKE {legacyPattern})");
            }

            await using var resource = typeof(RecitationCatalogService).Assembly.GetManifestResourceStream("QuranVideo.SaberAudioLinks")!;
            var defaults = (await JsonSerializer.DeserializeAsync<List<SeedLink>>(resource))!.ToDictionary(x => x.SurahNumber, x => x.DownloadUrl);
            var references = WarshRecitationReferential.All.ToDictionary(r => r.CollectionId);
            var collections = await db.RecitationCollections.ToListAsync();
            foreach (var collection in collections)
            {
                var reference = references.GetValueOrDefault(collection.Id);
                for (int n = 1; n <= 114; n++)
                {
                    var url = collection.Id == SaberCollection ? defaults[n] : "";
                    if (reference != null)
                    {
                        url = string.Format(reference.UrlTemplate, n);
                    }
                    // Preserve old configured collections when their template is usable.
                    else if (url.Length == 0 && !string.IsNullOrEmpty(collection.BaseUrlTemplate))
                    {
                        try { url = string.Format(collection.BaseUrlTemplate, n); }
                        catch (FormatException) { }
                        if (!IsValidUrl(url)) url = "";
                    }
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO audio_download_links (CollectionId,SurahNumber,Url) VALUES ({collection.Id},{n},{url})");
                    if (reference != null)
                    {
                        // Remplit les liens vides et remplace les anciens liens morts ; un lien saisi à la main est conservé
                        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audio_download_links SET Url={url} WHERE CollectionId={collection.Id} AND SurahNumber={n} AND (Url IS NULL OR Url='' OR Url LIKE {legacyPattern})");
                    }
                }
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO audio_catalog_preferences (Id,DefaultCollectionId) VALUES (1,{SaberCollection})");
            await tx.CommitAsync();
        }
        finally { InitializationGate.Release(); }
    }

    public async Task<List<RecitationChoice>> GetCollectionsAsync()
    {
        using var db = Db();
        return await db.RecitationCollections.Include(c => c.Reciter)
            .OrderBy(c => c.Id == SaberCollection ? 0 : (c.Id == HusaryCollection ? 1 : 2))
            .ThenBy(c => c.Reciter!.NameArabic)
            .Select(c => new RecitationChoice(c.Id, c.ReciterId, !string.IsNullOrEmpty(c.TitleArabic) ? c.TitleArabic : c.Reciter!.NameArabic)).ToListAsync();
    }

    public async Task<string> GetDefaultAsync()
    {
        using var db = Db();
        return await db.Database.SqlQueryRaw<string>("SELECT DefaultCollectionId AS Value FROM audio_catalog_preferences WHERE Id=1").SingleAsync();
    }

    public async Task SetDefaultAsync(string collectionId)
    {
        using var db = Db();
        if (!await db.RecitationCollections.AnyAsync(c => c.Id == collectionId)) throw new InvalidOperationException("القارئ غير موجود.");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audio_catalog_preferences SET DefaultCollectionId={collectionId} WHERE Id=1");
    }

    public async Task<Dictionary<int, string>> GetLinksAsync(string collectionId)
    {
        using var db = Db();
        var links = await db.Database.SqlQuery<LinkRow>($"SELECT SurahNumber, Url FROM audio_download_links WHERE CollectionId={collectionId}").ToListAsync();
        return links.ToDictionary(x => x.SurahNumber, x => x.Url);
    }

    public async Task SaveLinksAsync(string collectionId, IReadOnlyDictionary<int, string> links)
    {
        foreach (var (number, url) in links)
            if (number is < 1 or > 114 || (!string.IsNullOrWhiteSpace(url) && !IsValidUrl(url.Trim())))
                throw new ArgumentException($"رابط السورة {number} غير صالح. أدخل رابطاً يبدأ بـ https:// أو http:// أو اتركه فارغاً.");
        using var db = Db();
        if (!await db.RecitationCollections.AnyAsync(c => c.Id == collectionId)) throw new InvalidOperationException("القارئ غير موجود.");
        using var tx = await db.Database.BeginTransactionAsync();
        foreach (var (number, url) in links)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO audio_download_links (CollectionId,SurahNumber,Url) VALUES ({collectionId},{number},{url.Trim()}) ON CONFLICT(CollectionId,SurahNumber) DO UPDATE SET Url=excluded.Url");
        await tx.CommitAsync();
    }

    public async Task<RecitationChoice> AddReciterAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("أدخل اسم القارئ أولاً.");
        var id = "reciter_" + Guid.NewGuid().ToString("N");
        var collection = id + "_warsh";
        using var db = Db();
        using var tx = await db.Database.BeginTransactionAsync();
        db.Reciters.Add(new ReciterEntity { Id = id, NameArabic = name.Trim(), NameLatin = name.Trim() });
        db.RecitationCollections.Add(new RecitationCollectionEntity { Id = collection, ReciterId = id, TitleArabic = name.Trim(), Riwaya = "warsh" });
        await db.SaveChangesAsync();
        for (int n = 1; n <= 114; n++)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO audio_download_links (CollectionId,SurahNumber,Url) VALUES ({collection},{n},'')");
        await tx.CommitAsync();
        return new(collection, id, name.Trim());
    }

    public async Task RenameAsync(string collectionId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("أدخل اسم القارئ.");
        using var db = Db();
        var collection = await db.RecitationCollections.Include(c => c.Reciter).SingleAsync(c => c.Id == collectionId);
        // Le titre affiché peut mentionner la riwaya ; le nom du récitateur ne garde que le nom
        collection.Reciter!.NameArabic = CleanReciterName(name);
        collection.TitleArabic = name.Trim();
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Retire d'un nom de récitateur les mentions de riwaya ou de mushaf (« (ورش عن نافع) »,
    /// « المصحف المرتل برواية ورش — … ») pour ne garder que le nom de la personne.
    /// </summary>
    public static string CleanReciterName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            trimmed, @"\s*\([^)]*(ورش|رواية|حفص|قالون|نافع)[^)]*\)", string.Empty);
        var parts = cleaned.Split(new[] { " — ", " – ", " - " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && !p.Contains("رواية") && !p.Contains("المصحف"))
            .ToList();
        var result = string.Join(" — ", parts).Trim();
        return result.Length > 0 ? result : trimmed;
    }

    public static bool IsValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
    private sealed record SeedLink(int SurahNumber, string DownloadUrl);
    public sealed class LinkRow { public int SurahNumber { get; set; } public string Url { get; set; } = ""; }
}
