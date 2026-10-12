using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace QuranVideo.Prototype.Audio;

public sealed record AudioBenchmarkResult(
    string AudioFile,
    long FileSizeBytes,
    double FFprobeDurationSec,
    double NAudioDurationSec,
    long NAudioTotalSamples,
    int SampleRate,
    int Channels,
    double SeekDiscrepancyMs,
    string EvaluationDecision);

public static class AudioPrecisionBenchmark
{
    public static AudioBenchmarkResult BenchmarkFile(string audioPath, double testSeekSec = 100.0)
    {
        var fi = new FileInfo(audioPath);
        if (!fi.Exists) throw new FileNotFoundException("Audio file not found", audioPath);

        // 1. Run FFprobe to get true container/stream duration
        var ffprobePsi = new ProcessStartInfo("ffprobe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        ffprobePsi.ArgumentList.Add("-v");
        ffprobePsi.ArgumentList.Add("error");
        ffprobePsi.ArgumentList.Add("-show_entries");
        ffprobePsi.ArgumentList.Add("format=duration");
        ffprobePsi.ArgumentList.Add("-of");
        ffprobePsi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        ffprobePsi.ArgumentList.Add(audioPath);

        using var ffprobeProc = Process.Start(ffprobePsi)!;
        var ffprobeOut = ffprobeProc.StandardOutput.ReadToEnd().Trim();
        ffprobeProc.WaitForExit();
        var ffprobeDuration = double.Parse(ffprobeOut, System.Globalization.CultureInfo.InvariantCulture);

        // 2. Open with NAudio Mp3FileReader or MediaFoundationReader
        using var reader = new MediaFoundationReader(audioPath);
        var naudioDuration = reader.TotalTime.TotalSeconds;
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
        var bytesPerFrame = bytesPerSample * channels;
        var totalSamples = reader.Length / bytesPerFrame;

        // 3. Test seek accuracy
        double targetSeek = Math.Min(testSeekSec, naudioDuration * 0.5);
        reader.CurrentTime = TimeSpan.FromSeconds(targetSeek);
        var actualTime = reader.CurrentTime.TotalSeconds;
        var discrepancyMs = Math.Abs(actualTime - targetSeek) * 1000.0;

        var decision = discrepancyMs <= 15.0
            ? "NAudio MediaFoundation direct seek is precise (<15ms discrepancy)"
            : "Direct seek discrepancy detected; buffered window or common PCM cache recommended for exact boundary editing";

        return new AudioBenchmarkResult(
            audioPath,
            fi.Length,
            ffprobeDuration,
            naudioDuration,
            totalSamples,
            sampleRate,
            channels,
            discrepancyMs,
            decision);
    }
}
