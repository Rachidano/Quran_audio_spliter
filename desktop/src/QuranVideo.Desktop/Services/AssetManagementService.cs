using System.IO;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed class AssetManagementService
{
    private readonly WorkspaceService _workspaceService;
    private readonly string _dbPath;

    public AssetManagementService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        _dbPath = _workspaceService.GetDatabasePath();
    }

    public async Task<UserAssetEntity> ImportAssetAsync(
        string sourceFilePath,
        UserAssetType assetType,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Fichier source introuvable", sourceFilePath);

        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowedExts.Contains(ext))
            throw new InvalidOperationException($"Format de fichier non supporté ({ext}). Formats acceptés : PNG, JPG, JPEG, WEBP.");

        var sha256 = ComputeSha256(sourceFilePath);
        var originalFileName = Path.GetFileName(sourceFilePath);
        var safeBaseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFilePath));
        if (string.IsNullOrWhiteSpace(safeBaseName)) safeBaseName = "asset";

        // Déterminer le sous-dossier dans l'espace de travail
        var subDir = assetType == UserAssetType.Background ? "backgrounds" : "logos";
        var targetDir = Path.Combine(_workspaceService.WorkspacePath, "media", "assets", subDir);
        Directory.CreateDirectory(targetDir);

        var targetFileName = $"{sha256[..12]}_{safeBaseName}{ext}";
        var targetFullPath = Path.Combine(targetDir, targetFileName);
        var relativePath = Path.Combine("media", "assets", subDir, targetFileName).Replace('\\', '/');

        // Copie sécurisée si le fichier n'existe pas encore à destination
        if (!File.Exists(targetFullPath))
        {
            var tempTarget = targetFullPath + ".tmp";
            File.Copy(sourceFilePath, tempTarget, overwrite: true);
            File.Move(tempTarget, targetFullPath, overwrite: true);
        }

        // Enregistrement dans la base SQLite
        using var db = new AppDbContext(_dbPath);
        var existing = await db.UserAssets
            .FirstOrDefaultAsync(a => a.AssetType == assetType && a.Sha256 == sha256, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var asset = new UserAssetEntity
        {
            AssetType = assetType,
            RelativePath = relativePath,
            OriginalFileName = originalFileName,
            Sha256 = sha256,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.UserAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);

        return asset;
    }

    public async Task<List<UserAssetEntity>> GetAssetsAsync(
        UserAssetType? assetType = null,
        CancellationToken cancellationToken = default)
    {
        using var db = new AppDbContext(_dbPath);
        var query = db.UserAssets.AsQueryable();
        if (assetType.HasValue)
        {
            query = query.Where(a => a.AssetType == assetType.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAssetAsync(int assetId, CancellationToken cancellationToken = default)
    {
        using var db = new AppDbContext(_dbPath);
        var asset = await db.UserAssets.FindAsync(new object[] { assetId }, cancellationToken);
        if (asset == null) return false;

        var fullPath = Path.Combine(_workspaceService.WorkspacePath, asset.RelativePath);
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { /* ignore */ }
        }

        db.UserAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c) && !char.IsWhiteSpace(c)));
    }
}
