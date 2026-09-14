using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Quran.Application.Interfaces;

namespace Quran.AudioProcessor.Services;

public partial class FfmpegAudioProcessor : IAudioProcessorService
{
    private readonly ILogger<FfmpegAudioProcessor> _logger;
    private readonly string _ffmpegPath;

    [GeneratedRegex(@"Duration:\s*(\d{2}):(\d{2}):(\d{2}\.\d{2})")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"bitrate:\s*(\d+)\s*kb/s")]
    private static partial Regex BitrateRegex();

    [GeneratedRegex(@"silence_start:\s*([0-9\.]+)")]
    private static partial Regex SilenceStartRegex();

    public FfmpegAudioProcessor(ILogger<FfmpegAudioProcessor> logger, string ffmpegPath = "ffmpeg")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    public async Task<AudioProbeResult> ProbeAudioAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Fichier audio non trouvé.", filePath);

        var fileInfo = new FileInfo(filePath);
        var checksum = ComputeChecksum(filePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var duration = TimeSpan.Zero;
        var durMatch = DurationRegex().Match(stderr);
        if (durMatch.Success)
        {
            var hours = int.Parse(durMatch.Groups[1].Value);
            var minutes = int.Parse(durMatch.Groups[2].Value);
            var seconds = double.Parse(durMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }

        int bitrate = 128;
        var brMatch = BitrateRegex().Match(stderr);
        if (brMatch.Success && int.TryParse(brMatch.Groups[1].Value, out var parsedBr))
        {
            bitrate = parsedBr;
        }

        return new AudioProbeResult(
            Duration: duration,
            SizeBytes: fileInfo.Length,
            Bitrate: bitrate,
            MimeType: "audio/mpeg",
            Checksum: checksum
        );
    }

    public async Task<string> ExtractSegmentAsync(
        string sourcePath,
        string outputPath,
        TimeSpan startTime,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source audio non trouvée.", sourcePath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var startSeconds = startTime.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var durationSeconds = duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

        var arguments = $"-y -ss {startSeconds} -i \"{sourcePath}\" -t {durationSeconds} -c:a libmp3lame -b:a 128k \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg extraction failed (code {Code}): {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"FFmpeg a échoué avec le code {process.ExitCode}");
        }

        _logger.LogInformation("Successfully extracted segment from {Start} (dur: {Dur}) to {Path}", startTime, duration, outputPath);
        return outputPath;
    }

    public async Task<IReadOnlyList<TimeSpan>> DetectSilencesAsync(
        string filePath,
        double noiseDb = -30,
        double minDurationSeconds = 0.5,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Fichier introuvable.", filePath);

        var noiseStr = noiseDb.ToString(CultureInfo.InvariantCulture);
        var durStr = minDurationSeconds.ToString(CultureInfo.InvariantCulture);

        var arguments = $"-i \"{filePath}\" -af silencedetect=noise={noiseStr}dB:d={durStr} -f null -";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var silences = new List<TimeSpan>();
        var matches = SilenceStartRegex().Matches(stderr);
        foreach (Match m in matches)
        {
            if (double.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var sec))
            {
                silences.Add(TimeSpan.FromSeconds(sec));
            }
        }

        return silences;
    }

    public string ComputeChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
