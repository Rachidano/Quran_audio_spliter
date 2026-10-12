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
            using var tx = await db.Database.BeginTransactionAsync();
            if (!await db.Reciters.AnyAsync(r => r.Id == "saber_abdelhakam"))
                db.Reciters.Add(new ReciterEntity { Id = "saber_abdelhakam", NameArabic = "الشيخ صابر الحكم", NameLatin = "Saber Abdelhakam" });
            if (!await db.RecitationCollections.AnyAsync(c => c.Id == SaberCollection))
                db.RecitationCollections.Add(new RecitationCollectionEntity { Id = SaberCollection, ReciterId = "saber_abdelhakam", TitleArabic = "الشيخ صابر الحكم", Riwaya = "warsh" });
            await db.SaveChangesAsync();
            await using var resource = typeof(RecitationCatalogService).Assembly.GetManifestResourceStream("QuranVideo.SaberAudioLinks")!;
            var defaults = (await JsonSerializer.DeserializeAsync<List<SeedLink>>(resource))!.ToDictionary(x => x.SurahNumber, x => x.DownloadUrl);
            var collections = await db.RecitationCollections.ToListAsync();
            foreach (var collection in collections)
            {
                for (int n = 1; n <= 114; n++)
                {
                    var url = collection.Id == SaberCollection ? defaults[n] : "";
                    // Preserve old configured collections when their template is usable.
                    if (url.Length == 0 && !string.IsNullOrEmpty(collection.BaseUrlTemplate))
                    {
                        try { url = string.Format(collection.BaseUrlTemplate, n); }
                        catch (FormatException) { }
                        if (!IsValidUrl(url)) url = "";
                    }
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO audio_download_links (CollectionId,SurahNumber,Url) VALUES ({collection.Id},{n},{url})");
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
        return await db.RecitationCollections.Include(c => c.Reciter).OrderBy(c => c.Id == SaberCollection ? 0 : 1)
            .ThenBy(c => c.Reciter!.NameArabic)
            .Select(c => new RecitationChoice(c.Id, c.ReciterId, c.Reciter!.NameArabic)).ToListAsync();
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
        collection.Reciter!.NameArabic = name.Trim();
        collection.TitleArabic = name.Trim();
        await db.SaveChangesAsync();
    }

    public static bool IsValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
    private sealed record SeedLink(int SurahNumber, string DownloadUrl);
    public sealed class LinkRow { public int SurahNumber { get; set; } public string Url { get; set; } = ""; }
}
