using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class BackupTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly BackupService _backupService;
    private readonly string _backupZipPath;
    private readonly string _restoreTargetDir;

    public BackupTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_backup_src_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _backupService = new BackupService(_workspaceService);

        _backupZipPath = Path.Combine(Path.GetTempPath(), $"quran_test_backup_{Guid.NewGuid():N}.zip");
        _restoreTargetDir = Path.Combine(Path.GetTempPath(), $"quran_test_restore_dst_{Guid.NewGuid():N}");

        // Seed a real SQLite database with sample data
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        db.Database.EnsureCreated();
        db.Surahs.Add(new SurahEntity
        {
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            StartPage = 1,
            EndPage = 1
        });
        db.SaveChanges();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Seed a dummy media file
        var mediaDir = Path.Combine(_testWorkspaceDir, "media", "images");
        Directory.CreateDirectory(mediaDir);
        File.WriteAllText(Path.Combine(mediaDir, "test.txt"), "image test content");
    }

    [Fact]
    public async Task CreateBackup_CreatesValidZipWithOnlineSqliteSnapshotAndMetadata()
    {
        var result = await _backupService.CreateBackupAsync(_backupZipPath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(_backupZipPath));
        Assert.True(result.TotalBytes > 0);
        Assert.True(result.FileCount >= 2);
    }

    [Fact]
    public async Task RestoreBackup_RestoresWorkspaceAndOpensDbAccurately()
    {
        // 1. Create backup first
        var backupRes = await _backupService.CreateBackupAsync(_backupZipPath);
        Assert.True(backupRes.Success);

        // 2. Restore to new folder
        var restoreRes = await _backupService.RestoreBackupAsync(_backupZipPath, _restoreTargetDir);
        Assert.True(restoreRes.Success, restoreRes.ErrorMessage);
        Assert.True(Directory.Exists(_restoreTargetDir));

        // 3. Verify restored files
        var restoredDbPath = Path.Combine(_restoreTargetDir, "data", "quran.db");
        Assert.True(File.Exists(restoredDbPath));

        var restoredMediaFile = Path.Combine(_restoreTargetDir, "media", "images", "test.txt");
        Assert.True(File.Exists(restoredMediaFile));
        Assert.Equal("image test content", await File.ReadAllTextAsync(restoredMediaFile));

        // 4. Verify SQLite schema & query
        using var db = new AppDbContext(restoredDbPath);
        var surah = db.Surahs.Find(1);
        Assert.NotNull(surah);
        Assert.Equal("الفاتحة", surah.NameArabic);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_backupZipPath)) File.Delete(_backupZipPath);
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, recursive: true);
            if (Directory.Exists(_restoreTargetDir)) Directory.Delete(_restoreTargetDir, recursive: true);
        }
        catch
        {
        }
    }
}
