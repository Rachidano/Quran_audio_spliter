using System.IO;
using System.Text.Json;
using QuranVideo.Desktop.Models;

namespace QuranVideo.Desktop.Services;

public sealed class WorkspaceService : IDisposable
{
    private FileStream? _lockFileStream;
    public string WorkspacePath { get; private set; } = string.Empty;
    public bool IsLocked => _lockFileStream != null;

    public static string GetDefaultWorkspacePath()
    {
        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(myDocs, "QuranVideo");
    }

    public string GetDatabasePath() => Path.Combine(WorkspacePath, "data", "quran.db");

    public (bool Success, string? ErrorMessage) InitializeWorkspace(string? customPath = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(customPath)
            ? GetDefaultWorkspacePath()
            : Path.GetFullPath(customPath);

        try
        {
            Directory.CreateDirectory(targetPath);
            Directory.CreateDirectory(Path.Combine(targetPath, "data"));
            Directory.CreateDirectory(Path.Combine(targetPath, "media", "mushaf"));
            Directory.CreateDirectory(Path.Combine(targetPath, "media", "audio"));
            Directory.CreateDirectory(Path.Combine(targetPath, "media", "images"));
            Directory.CreateDirectory(Path.Combine(targetPath, "media", "videos"));
            Directory.CreateDirectory(Path.Combine(targetPath, "logs"));

            // Acquire exclusive lock file
            var lockFilePath = Path.Combine(targetPath, "quran_workspace.lock");
            _lockFileStream?.Dispose();
            _lockFileStream = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            WorkspacePath = targetPath;
            LoggingService.Instance.Initialize(targetPath);
            LoggingService.Instance.LogInfo($"Espace de travail initialisé et verrouillé : {targetPath}");

            return (true, null);
        }
        catch (IOException ex)
        {
            var msg = "مجلد العمل مستخدم حالياً من برنامج آخر أو قيد القفل. يرجى إغلاق البرنامج الآخر أولاً.";
            LoggingService.Instance.LogError($"Verrouillage échoué pour {targetPath}", ex);
            return (false, msg);
        }
        catch (UnauthorizedAccessException ex)
        {
            var msg = "لا تتوفر صلاحيات كافية للكتابة في هذا المجلد. يرجى اختيار مجلد آخر.";
            LoggingService.Instance.LogError($"Accès refusé pour {targetPath}", ex);
            return (false, msg);
        }
        catch (Exception ex)
        {
            var msg = $"حدث خطأ أثناء فتح مجلد العمل : {ex.Message}";
            LoggingService.Instance.LogError($"Erreur ouverture espace {targetPath}", ex);
            return (false, msg);
        }
    }

    public async Task SaveSessionStateAsync(SessionState state)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath)) return;
        try
        {
            state.LastUpdatedUtc = DateTime.UtcNow;
            var sessionFile = Path.Combine(WorkspacePath, "config.json");
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(sessionFile, json);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Impossible de sauvegarder la session : {ex.Message}");
        }
    }

    public async Task<SessionState> LoadSessionStateAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath)) return new SessionState();
        try
        {
            var sessionFile = Path.Combine(WorkspacePath, "config.json");
            if (File.Exists(sessionFile))
            {
                var json = await File.ReadAllTextAsync(sessionFile);
                var state = JsonSerializer.Deserialize<SessionState>(json);
                if (state != null) return state;
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Impossible de charger la session : {ex.Message}");
        }
        return new SessionState();
    }

    public string ToRelativePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath)) return absolutePath;
        var fullTarget = Path.GetFullPath(absolutePath);
        var fullWorkspace = Path.GetFullPath(WorkspacePath);
        if (fullTarget.StartsWith(fullWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            var rel = fullTarget.Substring(fullWorkspace.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace('\\', '/');
        }
        return absolutePath;
    }

    public string ToAbsolutePath(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath)) return relativeOrAbsolutePath;
        if (Path.IsPathRooted(relativeOrAbsolutePath)) return relativeOrAbsolutePath;
        var normalized = relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(WorkspacePath, normalized);
    }

    public void Dispose()
    {
        if (_lockFileStream != null)
        {
            try
            {
                _lockFileStream.Dispose();
                _lockFileStream = null;
                var lockPath = Path.Combine(WorkspacePath, "quran_workspace.lock");
                if (File.Exists(lockPath)) File.Delete(lockPath);
            }
            catch
            {
            }
        }
    }
}
