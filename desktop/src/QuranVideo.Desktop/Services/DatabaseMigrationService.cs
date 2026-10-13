using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed class DatabaseMigrationService
{
    public const int CurrentSchemaVersion = 3;

    public async Task<bool> MigrateDatabaseAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty", nameof(dbPath));

        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        var connectionString = $"Data Source={dbPath}";
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Lire la version de schéma actuelle et le nombre de tables
        int currentVersion = 0;
        int tableCount = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                currentVersion = Convert.ToInt32(result);
            }

            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // Si la base est vierge (aucune table), créer l'intégralité du modèle EF Core (V2 + V3)
        if (tableCount == 0)
        {
            connection.Close();
            using (var db = new AppDbContext(dbPath))
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
            await connection.OpenAsync(cancellationToken);
        }
        else if (currentVersion < CurrentSchemaVersion)
        {
            // 2. Base existante avec données : snapshot de sécurité obligatoire avant migration
            CreatePreMigrationBackup(dbPath);
        }

        // Si la base est déjà à jour (version >= 3)
        if (currentVersion >= CurrentSchemaVersion && tableCount > 0)
        {
            return true;
        }

        // 3. Exécuter la migration DDL de manière transactionnelle
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                // Création des tables V3 si elles n'existent pas
                cmd.CommandText = @"
-- 1. Table des ressources importées (fonds, logos)
CREATE TABLE IF NOT EXISTS user_assets (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AssetType INTEGER NOT NULL,
    RelativePath TEXT NOT NULL,
    OriginalFileName TEXT NOT NULL,
    Sha256 TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_user_assets_AssetType_Sha256 
ON user_assets(AssetType, Sha256);

-- 2. Table des projets / créations vidéo V3
CREATE TABLE IF NOT EXISTS video_projects (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    SurahNumber INTEGER NOT NULL,
    StartAyahNumber INTEGER NOT NULL,
    EndAyahNumber INTEGER NOT NULL,
    ReciterId TEXT NOT NULL,
    AudioSourceId TEXT NOT NULL,
    Orientation TEXT NOT NULL DEFAULT 'Horizontal',
    Background INTEGER NOT NULL DEFAULT 0,
    CustomBackgroundPath TEXT,
    Watermark INTEGER NOT NULL DEFAULT 0,
    WatermarkText TEXT,
    WatermarkImagePath TEXT,
    WatermarkPosition INTEGER NOT NULL DEFAULT 3,
    WatermarkOpacity REAL NOT NULL DEFAULT 0.85,
    WatermarkScale REAL NOT NULL DEFAULT 1.0,
    IsWatermarkEnabled INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Draft',
    OutputVideoPath TEXT,
    ErrorMessage TEXT,
    LastRenderedAtUtc TEXT,
    Revision INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_video_projects_Surah_Range 
ON video_projects(SurahNumber, StartAyahNumber, EndAyahNumber);

-- 3. Table des versets composant chaque projet (séquence)
CREATE TABLE IF NOT EXISTS project_ayahs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER NOT NULL,
    OrderIndex INTEGER NOT NULL,
    SurahNumber INTEGER NOT NULL,
    AyahNumber INTEGER NOT NULL,
    AyahId TEXT NOT NULL,
    PassageId INTEGER NOT NULL,
    PassageRevision INTEGER NOT NULL,
    StartTimeSec REAL NOT NULL,
    EndTimeSec REAL NOT NULL,
    ImageRecipeId INTEGER NOT NULL,
    ImageRevision INTEGER NOT NULL,
    RelativeImagePath TEXT NOT NULL,
    JunctionReviewed INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(ProjectId) REFERENCES video_projects(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_project_ayahs_Project_Order 
ON project_ayahs(ProjectId, OrderIndex);

-- 4. Table des collections thématiques
CREATE TABLE IF NOT EXISTS collections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

-- 5. Table de liaison des éléments d'une collection
CREATE TABLE IF NOT EXISTS collection_items (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CollectionId INTEGER NOT NULL,
    ProjectId INTEGER NOT NULL,
    OrderIndex INTEGER NOT NULL,
    AddedAtUtc TEXT NOT NULL,
    FOREIGN KEY(CollectionId) REFERENCES collections(Id) ON DELETE CASCADE,
    FOREIGN KEY(ProjectId) REFERENCES video_projects(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_collection_items_Collection_Project 
ON collection_items(CollectionId, ProjectId);

-- Mettre à jour la version de schéma
PRAGMA user_version = 3;
";
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                await EnsureColumnExistsAsync(connection, transaction, "video_projects", "Status", "TEXT NOT NULL DEFAULT 'Draft'", cancellationToken);
                await EnsureColumnExistsAsync(connection, transaction, "video_projects", "OutputVideoPath", "TEXT", cancellationToken);
                await EnsureColumnExistsAsync(connection, transaction, "video_projects", "ErrorMessage", "TEXT", cancellationToken);
                await EnsureColumnExistsAsync(connection, transaction, "video_projects", "LastRenderedAtUtc", "TEXT", cancellationToken);
                await EnsureColumnExistsAsync(connection, transaction, "video_projects", "WatermarkScale", "REAL NOT NULL DEFAULT 1.0", cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static void CreatePreMigrationBackup(string dbPath)
    {
        var backupPath = dbPath + ".pre_v3.bak";
        if (File.Exists(backupPath))
        {
            // Vérifier si la sauvegarde préexistante est une base SQLite valide
            bool isExistingBackupValid = false;
            try
            {
                using var testConn = new SqliteConnection($"Data Source={backupPath};Pooling=False");
                testConn.Open();
                using var cmd = testConn.CreateCommand();
                cmd.CommandText = "PRAGMA schema_version;";
                var ver = cmd.ExecuteScalar();
                isExistingBackupValid = ver != null;
            }
            catch
            {
                isExistingBackupValid = false;
            }

            if (!isExistingBackupValid)
            {
                throw new InvalidOperationException(
                    $"Le fichier de sauvegarde pré-migration existant ({backupPath}) est corrompu ou n'est pas une base SQLite valide. La migration ne peut pas continuer.");
            }

            return;
        }

        try
        {
            SqliteConnection.ClearAllPools();
            using var sourceConn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            sourceConn.Open();
            using var destConn = new SqliteConnection($"Data Source={backupPath};Pooling=False");
            destConn.Open();
            sourceConn.BackupDatabase(destConn);
            SqliteConnection.ClearAllPools();

            if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
            {
                throw new InvalidOperationException($"Le fichier de sauvegarde {backupPath} est vide ou invalide.");
            }

            // Vérification immédiate de la validité de la sauvegarde créée
            using var verifyConn = new SqliteConnection($"Data Source={backupPath};Pooling=False");
            verifyConn.Open();
            using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = "PRAGMA schema_version;";
            verifyCmd.ExecuteScalar();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            LoggingService.Instance.LogError($"Échec critique: Impossible de créer la sauvegarde pré-migration de {dbPath}", ex);
            throw new InvalidOperationException($"Échec de la sauvegarde préalable avant migration V3: {ex.Message}", ex);
        }
    }

    private static async Task EnsureColumnExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        using var checkCmd = connection.CreateCommand();
        checkCmd.Transaction = transaction;
        checkCmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
        bool exists = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        await reader.CloseAsync();

        if (!exists)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.Transaction = transaction;
            alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
