using System.IO;
using System.Text.Json;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class WorkspaceTests : IDisposable
{
    private readonly string _testWorkspaceDir;

    public WorkspaceTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_workspace_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspaceDir))
            {
                Directory.Delete(_testWorkspaceDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void InitializeWorkspace_CreatesRequiredDirectoryStructure()
    {
        using var ws = new WorkspaceService();
        var (success, error) = ws.InitializeWorkspace(_testWorkspaceDir);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(ws.IsLocked);

        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "data")));
        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "media", "mushaf")));
        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "media", "audio")));
        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "media", "images")));
        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "media", "videos")));
        Assert.True(Directory.Exists(Path.Combine(_testWorkspaceDir, "logs")));
        Assert.True(File.Exists(Path.Combine(_testWorkspaceDir, "quran_workspace.lock")));
    }

    [Fact]
    public void InitializeWorkspace_WhenAlreadyLockedByAnotherInstance_ReturnsFailureWithArabicMessage()
    {
        using var ws1 = new WorkspaceService();
        var (success1, _) = ws1.InitializeWorkspace(_testWorkspaceDir);
        Assert.True(success1);

        using var ws2 = new WorkspaceService();
        var (success2, error2) = ws2.InitializeWorkspace(_testWorkspaceDir);

        Assert.False(success2);
        Assert.NotNull(error2);
        Assert.Contains("مجلد العمل مستخدم", error2);
    }

    [Fact]
    public async Task SessionState_SavesAndRestoresCorrectly()
    {
        using var ws = new WorkspaceService();
        ws.InitializeWorkspace(_testWorkspaceDir);

        var stateToSave = new SessionState
        {
            ActiveView = "Studio",
            LastSurahNumber = 2,
            LastAyahNumber = 255,
            LastOrientation = "Vertical"
        };

        await ws.SaveSessionStateAsync(stateToSave);

        var loadedState = await ws.LoadSessionStateAsync();

        Assert.Equal("Studio", loadedState.ActiveView);
        Assert.Equal(2, loadedState.LastSurahNumber);
        Assert.Equal(255, loadedState.LastAyahNumber);
        Assert.Equal("Vertical", loadedState.LastOrientation);
    }

    [Fact]
    public async Task ExportDiagnostics_GeneratesCompleteJsonWithoutMediaFiles()
    {
        using var ws = new WorkspaceService();
        ws.InitializeWorkspace(_testWorkspaceDir);

        LoggingService.Instance.LogInfo("Test message for diagnostic");
        var diagPath = Path.Combine(_testWorkspaceDir, "diagnostic.json");

        var json = await LoggingService.Instance.ExportDiagnosticsAsync(_testWorkspaceDir, diagPath);

        Assert.True(File.Exists(diagPath));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("QuranVideo Desktop", root.GetProperty("Application").GetString());
        Assert.Equal(LoggingService.AppVersion, root.GetProperty("Version").GetString());
        Assert.True(root.TryGetProperty("Environment", out _));
        Assert.True(root.TryGetProperty("Workspace", out _));
        Assert.True(root.TryGetProperty("RecentLogs", out _));
        Assert.Contains("Test message for diagnostic", root.GetProperty("RecentLogs").GetString());
    }

    [Fact]
    public void SurahCatalogService_Contains114SurahsWithWarshBaqarah285Ayahs()
    {
        var surahs = SurahCatalogService.GetAllSurahs();
        Assert.Equal(114, surahs.Count);

        var fatiha = SurahCatalogService.GetSurah(1);
        Assert.NotNull(fatiha);
        Assert.Equal("الفاتحة", fatiha.NameArabic);
        Assert.Equal(7, fatiha.AyahCount);
        Assert.Equal(1, fatiha.StartPage);
        Assert.Equal(1, fatiha.EndPage);

        var baqara = SurahCatalogService.GetSurah(2);
        Assert.NotNull(baqara);
        Assert.Equal("البقرة", baqara.NameArabic);
        Assert.Equal(285, baqara.AyahCount); // Canonical Warsh
        Assert.Equal(2, baqara.StartPage);
        Assert.Equal(49, baqara.EndPage);

        var nas = SurahCatalogService.GetSurah(114);
        Assert.NotNull(nas);
        Assert.Equal("الناس", nas.NameArabic);
        Assert.Equal(6, nas.AyahCount);
        Assert.Equal(604, nas.StartPage);
        Assert.Equal(604, nas.EndPage);
    }
}
