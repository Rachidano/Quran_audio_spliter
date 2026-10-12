using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace QuranVideo.Desktop.Services;

public record BackupResult(bool Success, string? ErrorMessage, string? BackupFilePath, long TotalBytes, int FileCount);
public record RestoreResult(bool Success, string? ErrorMessage, string? RestoredWorkspacePath, int RestoredFileCount);

public sealed class BackupService
{
    private readonly WorkspaceService _workspaceService;

    public BackupService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// L10-01 & L10-02: Sauvegarde cohérente du workspace avec l'API SQLite Online Backup
    /// et compression des médias dans une archive zip unique.
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(
        string targetZipPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var workspacePath = _workspaceService.WorkspacePath;
        var sourceDbPath = Path.Combine(workspacePath, "data", "quran.db");

        if (!Directory.Exists(workspacePath) || !File.Exists(sourceDbPath))
        {
            return new BackupResult(false, "مجلد العمل أو قاعدة البيانات غير متوفرة.", null, 0, 0);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"quran_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            progress?.Report(10);

            // 1. Snapshot cohérent de SQLite via Online Backup API (L10-01)
            var tempDbDir = Path.Combine(tempDir, "data");
            Directory.CreateDirectory(tempDbDir);
            var snapshotDbPath = Path.Combine(tempDbDir, "quran.db");

            using (var sourceConn = new SqliteConnection($"Data Source={sourceDbPath};Pooling=False"))
            {
                await sourceConn.OpenAsync(cancellationToken);
                using (var destConn = new SqliteConnection($"Data Source={snapshotDbPath};Pooling=False"))
                {
                    await destConn.OpenAsync(cancellationToken);
                    sourceConn.BackupDatabase(destConn);
                }
            }

            SqliteConnection.ClearAllPools();
            progress?.Report(30);

            // 2. Copier manifest.json et session.json
            var manifestSource = Path.Combine(workspacePath, "manifest.json");
            if (File.Exists(manifestSource))
            {
                File.Copy(manifestSource, Path.Combine(tempDir, "manifest.json"), overwrite: true);
            }

            var configFile = Path.Combine(workspacePath, "config.json");
            if (File.Exists(configFile))
            {
                File.Copy(configFile, Path.Combine(tempDir, "config.json"), overwrite: true);
            }

            var sessionSource = Path.Combine(workspacePath, "session.json");
            if (File.Exists(sessionSource))
            {
                File.Copy(sessionSource, Path.Combine(tempDir, "session.json"), overwrite: true);
            }

            // 3. Copier les dossiers médias pertinents (L10-01)
            var mediaSource = Path.Combine(workspacePath, "media");
            if (Directory.Exists(mediaSource))
            {
                CopyDirectoryRecursive(mediaSource, Path.Combine(tempDir, "media"));
            }

            progress?.Report(50);

            // 4. Générer le manifeste de sauvegarde (L10-01)
            var dbSha256 = ComputeSha256(snapshotDbPath);
            var allFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
            var totalBytes = allFiles.Sum(f => new FileInfo(f).Length);

            var meta = new BackupMetadata
            {
                BackupVersion = "1.0",
                AppVersion = LoggingService.AppVersion,
                CreatedAtUtc = DateTime.UtcNow,
                DatabaseSha256 = dbSha256,
                TotalFiles = allFiles.Length,
                TotalBytes = totalBytes
            };

            var metaPath = Path.Combine(tempDir, "backup_manifest.json");
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

            progress?.Report(70);

            // 5. Compresser dans l'archive zip finale
            var targetDir = Path.GetDirectoryName(targetZipPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(targetZipPath))
            {
                File.Delete(targetZipPath);
            }

            ZipFile.CreateFromDirectory(tempDir, targetZipPath, CompressionLevel.Optimal, false);
            progress?.Report(100);

            LoggingService.Instance.LogInfo($"Sauvegarde créée avec succès : {targetZipPath} ({allFiles.Length} fichiers, {totalBytes} octets)");
            return new BackupResult(true, null, targetZipPath, totalBytes, allFiles.Length);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Erreur création sauvegarde", ex);
            return new BackupResult(false, $"فشل إنشاء النسخة الاحتياطية : {ex.Message}", null, 0, 0);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Nettoyage temporaire silencieux
            }
        }
    }

    /// <summary>
    /// L10-03: Restauration complète d'un workspace depuis une archive de sauvegarde.
    /// </summary>
    public async Task<RestoreResult> RestoreBackupAsync(
        string backupZipPath,
        string targetWorkspacePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupZipPath))
        {
            return new RestoreResult(false, "ملف النسخة الاحتياطية غير موجود.", null, 0);
        }

        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"quran_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);

        try
        {
            await Task.Yield();
            progress?.Report(20);

            // 1. Extraire dans un dossier temporaire d'abord pour vérifier l'intégrité
            ZipFile.ExtractToDirectory(backupZipPath, tempExtractDir, overwriteFiles: true);

            var metaPath = Path.Combine(tempExtractDir, "backup_manifest.json");
            if (!File.Exists(metaPath))
            {
                return new RestoreResult(false, "الملف المضغوط لا يحتوي على بيان النسخة الاحتياطية الصحيح.", null, 0);
            }

            var dbPath = Path.Combine(tempExtractDir, "data", "quran.db");
            if (!File.Exists(dbPath))
            {
                return new RestoreResult(false, "الملف المضغوط لا يحتوي على قاعدة بيانات القرآن الكريم.", null, 0);
            }

            // Vérification cryptographique de l'intégrité de la base de données (A06)
            var metaJson = await File.ReadAllTextAsync(metaPath, cancellationToken);
            var meta = JsonSerializer.Deserialize<BackupMetadata>(metaJson);
            if (meta == null || string.IsNullOrWhiteSpace(meta.DatabaseSha256))
            {
                return new RestoreResult(false, "بيان النسخة الاحتياطية غير صالح أو معطوب.", null, 0);
            }

            var actualDbSha = ComputeSha256(dbPath);
            if (!string.Equals(actualDbSha, meta.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
            {
                LoggingService.Instance.LogError($"Restauration rejetée: intégrité de la base compromise (obtenu {actualDbSha}, attendu {meta.DatabaseSha256})");
                return new RestoreResult(false, "فشل التحقق من سلامة قاعدة البيانات: تم العبث بمحتوى الملف أو أنه تالف.", null, 0);
            }

            progress?.Report(50);

            // 2. Déployer vers le workspace cible
            Directory.CreateDirectory(targetWorkspacePath);
            CopyDirectoryRecursive(tempExtractDir, targetWorkspacePath);

            var restoredFiles = Directory.GetFiles(targetWorkspacePath, "*.*", SearchOption.AllDirectories);
            progress?.Report(100);

            LoggingService.Instance.LogInfo($"Restauration effectuée vers {targetWorkspacePath} ({restoredFiles.Length} fichiers)");
            return new RestoreResult(true, null, targetWorkspacePath, restoredFiles.Length);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Erreur restauration", ex);
            return new RestoreResult(false, $"فشل استرجاع النسخة الاحتياطية : {ex.Message}", null, 0);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, dest);
        }
    }

    private static string ComputeSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}

public sealed class BackupMetadata
{
    [JsonPropertyName("backupVersion")]
    public string BackupVersion { get; set; } = "1.0";

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("databaseSha256")]
    public string DatabaseSha256 { get; set; } = string.Empty;

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }
}
