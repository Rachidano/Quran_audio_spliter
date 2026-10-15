using System.Diagnostics;
using System.IO;
using System.Text;

namespace QuranVideo.Desktop.Services;

public sealed record ProcessRunResult(bool Success, int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

/// <summary>
/// Lance FFmpeg / FFprobe avec un délai maximal d'inactivité : si le processus n'écrit plus rien
/// (ni sur la sortie standard, ni sur la sortie d'erreur) pendant ce délai, il est arrêté.
/// FFmpeg écrit sa progression en continu, un fichier corrompu ne peut donc plus bloquer un export.
/// </summary>
public static class ExternalProcessRunner
{
    /// <summary>Délai d'inactivité par défaut pour FFmpeg (progression écrite plusieurs fois par seconde).</summary>
    public static TimeSpan FfmpegInactivityTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Délai d'inactivité par défaut pour FFprobe (lecture des métadonnées seulement).</summary>
    public static TimeSpan FfprobeInactivityTimeout { get; set; } = TimeSpan.FromSeconds(60);

    private const int MaxCapturedErrorChars = 16_000;

    public static async Task<ProcessRunResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan inactivityTimeout,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        long lastActivity = Environment.TickCount64;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        async Task PumpAsync(StreamReader reader, StringBuilder target, int? maxChars)
        {
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
            {
                Interlocked.Exchange(ref lastActivity, Environment.TickCount64);
                lock (target)
                {
                    target.Append(buffer, 0, read);
                    // Ne garder que la fin de la sortie d'erreur (progression FFmpeg très verbeuse)
                    if (maxChars is int max && target.Length > max * 2)
                        target.Remove(0, target.Length - max);
                }
            }
        }

        var stdoutTask = PumpAsync(process.StandardOutput, stdout, null);
        var stderrTask = PumpAsync(process.StandardError, stderr, MaxCapturedErrorChars);
        var exitTask = process.WaitForExitAsync();

        try
        {
            while (!exitTask.IsCompleted)
            {
                await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();

                var idleMs = Environment.TickCount64 - Interlocked.Read(ref lastActivity);
                if (!exitTask.IsCompleted && idleMs > inactivityTimeout.TotalMilliseconds)
                {
                    Kill(process);
                    await WaitQuietlyAsync(exitTask, stdoutTask, stderrTask);
                    var seconds = (int)Math.Round(inactivityTimeout.TotalSeconds);
                    LoggingService.Instance.LogWarning($"{Path.GetFileName(fileName)} arrêté après {seconds} s sans activité.");
                    return new ProcessRunResult(false, -1, Snapshot(stdout),
                        $"توقفت أداة المعالجة ({Path.GetFileNameWithoutExtension(fileName)}) عن الاستجابة لأكثر من {seconds} ثانية فأُوقفت. قد يكون الملف تالفاً.",
                        TimedOut: true);
                }
            }

            await exitTask;
            await Task.WhenAll(stdoutTask, stderrTask);
            return new ProcessRunResult(process.ExitCode == 0, process.ExitCode, Snapshot(stdout), Snapshot(stderr), TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await WaitQuietlyAsync(exitTask, stdoutTask, stderrTask);
            throw;
        }
    }

    private static string Snapshot(StringBuilder sb)
    {
        lock (sb) return sb.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Le processus s'est terminé entre-temps
        }
    }

    private static async Task WaitQuietlyAsync(params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Arrêt forcé : les flux peuvent se terminer en erreur
        }
    }
}
