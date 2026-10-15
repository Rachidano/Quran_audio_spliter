using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuranVideo.Desktop.Services;

public sealed class LoggingService
{
    private static LoggingService? _instance;
    public static LoggingService Instance => _instance ??= new LoggingService();

    public const string AppVersion = "4.0.0";
    private readonly object _lock = new();
    private string? _logFilePath;

    public void Initialize(string workspaceDirectory)
    {
        var logsDir = Path.Combine(workspaceDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        _logFilePath = Path.Combine(logsDir, "quran_video.log");
        LogInfo($"--- QuranVideo Desktop v{AppVersion} Démarré ---");
        LogInfo($"Système d'exploitation: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        LogInfo($".NET Runtime: {RuntimeInformation.FrameworkDescription}");
    }

    public void LogInfo(string message) => Log("INFO", message);
    public void LogWarning(string message) => Log("WARN", message);
    public void LogError(string message, Exception? ex = null)
    {
        var full = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        Log("ERROR", full);
    }

    private void Log(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        if (_logFilePath == null) return;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Never crash because of logging failure
            }
        }
    }

    public async Task<string> ExportDiagnosticsAsync(string workspaceDirectory, string outputPath)
    {
        var logsDir = Path.Combine(workspaceDirectory, "logs");
        var logContent = "";
        var logFile = Path.Combine(logsDir, "quran_video.log");
        if (File.Exists(logFile))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(logFile);
                logContent = string.Join(Environment.NewLine, lines.TakeLast(200));
            }
            catch (Exception ex)
            {
                logContent = $"Impossible de lire le journal : {ex.Message}";
            }
        }

        // Test ffmpeg presence
        var ffmpegInfo = await GetCommandVersionAsync("ffmpeg", "-version");
        var ffprobeInfo = await GetCommandVersionAsync("ffprobe", "-version");

        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(workspaceDirectory)) ?? "C:\\");
        var freeSpaceMb = drive.AvailableFreeSpace / (1024 * 1024);

        var report = new
        {
            Application = "QuranVideo Desktop",
            Version = AppVersion,
            GeneratedAt = DateTime.UtcNow.ToString("o"),
            Environment = new
            {
                OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                DotNet = RuntimeInformation.FrameworkDescription,
                MachineName = Environment.MachineName
            },
            Tools = new
            {
                Ffmpeg = ffmpegInfo,
                Ffprobe = ffprobeInfo
            },
            Workspace = new
            {
                Path = workspaceDirectory,
                FreeSpaceMb = freeSpaceMb,
                DirectoriesExist = new
                {
                    Logs = Directory.Exists(Path.Combine(workspaceDirectory, "logs")),
                    Data = Directory.Exists(Path.Combine(workspaceDirectory, "data")),
                    Media = Directory.Exists(Path.Combine(workspaceDirectory, "media")),
                    Videos = Directory.Exists(Path.Combine(workspaceDirectory, "media", "videos"))
                }
            },
            RecentLogs = logContent
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(report, options);
        await File.WriteAllTextAsync(outputPath, json);
        return json;
    }

    private static async Task<string> GetCommandVersionAsync(string command, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(command, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return "Non disponible";
            var output = await p.StandardOutput.ReadLineAsync();
            await p.WaitForExitAsync();
            return output ?? "Disponible (aucune sortie)";
        }
        catch (Exception ex)
        {
            return $"Introuvable ({ex.Message})";
        }
    }
}
