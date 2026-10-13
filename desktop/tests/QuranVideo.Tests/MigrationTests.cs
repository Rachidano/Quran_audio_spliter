using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class MigrationTests : IDisposable
{
    private readonly string _testDir;

    public MigrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"quran_test_migration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { /* ignore cleanup errors */ }
    }

    [Fact]
    public async Task MigrateDatabase_FreshDatabase_CreatesSchemaV3AndAllTables()
    {
        var dbPath = Path.Combine(_testDir, "fresh.db");
        var migrationService = new DatabaseMigrationService();

        var result = await migrationService.MigrateDatabaseAsync(dbPath);

        Assert.True(result);
        Assert.True(File.Exists(dbPath));

        using var db = new AppDbContext(dbPath);
        // Les tables V3 doivent pouvoir être interrogées sans exception
        Assert.Empty(await db.Projects.ToListAsync());
        Assert.Empty(await db.ProjectAyahs.ToListAsync());
        Assert.Empty(await db.Collections.ToListAsync());
        Assert.Empty(await db.CollectionItems.ToListAsync());
        Assert.Empty(await db.UserAssets.ToListAsync());

        // Vérifier le pragma user_version
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.Equal(3, version);
    }

    [Fact]
    public async Task MigrateDatabase_FromV2Baseline_PreservesAllExistingDataAndCreatesPreV3Backup()
    {
        var baselineDb = Path.GetFullPath(@"desktop/doc/validation/v3/baseline_workspace_v2/data/quran.db");
        if (!File.Exists(baselineDb))
        {
            baselineDb = Path.GetFullPath(@"D:\Projets\ITComp\Quran\desktop\doc\validation\v3\baseline_workspace_v2\data\quran.db");
        }
        Assert.True(File.Exists(baselineDb), $"Baseline V2 DB must exist at {baselineDb}");

        var targetDb = Path.Combine(_testDir, "migrated_v2.db");
        File.Copy(baselineDb, targetDb);

        var migrationService = new DatabaseMigrationService();
        var result = await migrationService.MigrateDatabaseAsync(targetDb);

        Assert.True(result);

        // 1. La sauvegarde de pré-migration doit exister
        var backupPath = targetDb + ".pre_v3.bak";
        Assert.True(File.Exists(backupPath), "Pre-migration backup .pre_v3.bak must exist");

        // 2. Les données V2 existantes doivent être intégralement préservées
        using var db = new AppDbContext(targetDb);
        Assert.Equal(1, await db.Editions.CountAsync());
        Assert.Equal(604, await db.Pages.CountAsync());
        Assert.Equal(114, await db.Surahs.CountAsync());
        Assert.Equal(6214, await db.Ayahs.CountAsync());
        Assert.Equal(6214, await db.Texts.CountAsync());
        Assert.Equal(12782, await db.Regions.CountAsync());
        Assert.Equal(2, await db.Reciters.CountAsync());
        Assert.Equal(2, await db.Passages.CountAsync());
        Assert.Equal(47, await db.ImageRecipes.CountAsync());

        // 3. Les nouvelles tables V3 doivent accepter des écritures
        var project = new VideoProjectEntity
        {
            Title = "سلسلة الفاتحة",
            SurahNumber = 1,
            StartAyahNumber = 1,
            EndAyahNumber = 3,
            ReciterId = "saber_abdelhakam_warsh",
            Orientation = "Horizontal",
            Background = BackgroundType.Dark
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        Assert.True(project.Id > 0);

        var projectAyah = new ProjectAyahEntity
        {
            ProjectId = project.Id,
            OrderIndex = 1,
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            PassageId = 1,
            PassageRevision = 1,
            StartTimeSec = 0.0,
            EndTimeSec = 5.8,
            ImageRecipeId = 1,
            ImageRevision = 1,
            RelativeImagePath = "media/images/ayahs/ayah_001_001_rev1.png"
        };
        db.ProjectAyahs.Add(projectAyah);

        var collection = new CollectionEntity
        {
            Name = "تلاوات مميزة",
            Description = "مجموعة تجريبية"
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        var colItem = new CollectionItemEntity
        {
            CollectionId = collection.Id,
            ProjectId = project.Id,
            OrderIndex = 1
        };
        db.CollectionItems.Add(colItem);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(1, await db.ProjectAyahs.CountAsync());
        Assert.Equal(1, await db.Collections.CountAsync());
        Assert.Equal(1, await db.CollectionItems.CountAsync());
    }

    [Fact]
    public async Task MigrateDatabase_IsIdempotent()
    {
        var dbPath = Path.Combine(_testDir, "idempotent.db");
        var migrationService = new DatabaseMigrationService();

        // 1er passage
        var r1 = await migrationService.MigrateDatabaseAsync(dbPath);
        Assert.True(r1);

        // 2ème passage
        var r2 = await migrationService.MigrateDatabaseAsync(dbPath);
        Assert.True(r2);

        using var db = new AppDbContext(dbPath);
        Assert.Empty(await db.Projects.ToListAsync());
    }

    [Fact]
    public async Task AssetManagementService_ImportAndRetrieveAssets_WorksAccurately()
    {
        var wsPath = Path.Combine(_testDir, "workspace");
        var wsService = new WorkspaceService();
        wsService.InitializeWorkspace(wsPath);

        // Migrer la base de l'espace de test
        var migrationService = new DatabaseMigrationService();
        await migrationService.MigrateDatabaseAsync(wsService.GetDatabasePath());

        var assetService = new AssetManagementService(wsService);

        // Créer une fausse image de test PNG
        var sampleImage = Path.Combine(_testDir, "test_logo.png");
        File.WriteAllBytes(sampleImage, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // Importer
        var imported = await assetService.ImportAssetAsync(sampleImage, UserAssetType.Logo);

        Assert.NotNull(imported);
        Assert.True(imported.Id > 0);
        Assert.Equal(UserAssetType.Logo, imported.AssetType);
        Assert.StartsWith("media/assets/logos/", imported.RelativePath);
        Assert.True(File.Exists(Path.Combine(wsPath, imported.RelativePath)));

        // Récupérer la liste
        var logos = await assetService.GetAssetsAsync(UserAssetType.Logo);
        Assert.Single(logos);

        // Supprimer
        var deleted = await assetService.DeleteAssetAsync(imported.Id);
        Assert.True(deleted);
        Assert.False(File.Exists(Path.Combine(wsPath, imported.RelativePath)));
    }
}
