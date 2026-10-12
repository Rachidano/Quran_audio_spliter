using System.IO;
using NAudio.Wave;

namespace QuranVideo.Desktop.Services;

public sealed class WaveformService
{
    private readonly WorkspaceService _workspaceService;

    public WaveformService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    public async Task<float[]> GetWaveformPeaksAsync(
        string audioFilePath,
        string sha256,
        int totalPoints = 800,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath)) return Array.Empty<float>();

        var cacheDir = Path.Combine(_workspaceService.WorkspacePath, "cache", "peaks");
        Directory.CreateDirectory(cacheDir);

        var cacheFile = Path.Combine(cacheDir, $"{sha256}_{totalPoints}.dat");
        if (File.Exists(cacheFile))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(cacheFile, cancellationToken);
                var cachedPeaks = new float[bytes.Length / sizeof(float)];
                Buffer.BlockCopy(bytes, 0, cachedPeaks, 0, bytes.Length);
                return cachedPeaks;
            }
            catch
            {
                // In case of corrupt cache file, recompute
            }
        }

        // Streaming generation in chunks: does not load entire file in memory
        var peaks = await Task.Run(() => ComputePeaksStreaming(audioFilePath, totalPoints, cancellationToken), cancellationToken);

        // Save to disk cache
        try
        {
            var bytes = new byte[peaks.Length * sizeof(float)];
            Buffer.BlockCopy(peaks, 0, bytes, 0, bytes.Length);
            await File.WriteAllBytesAsync(cacheFile, bytes, cancellationToken);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Impossible de cacher les peaks de forme d'onde: {ex.Message}");
        }

        return peaks;
    }

    private static float[] ComputePeaksStreaming(string audioFilePath, int totalPoints, CancellationToken cancellationToken)
    {
        using var reader = new MediaFoundationReader(audioFilePath);
        var sampleProvider = reader.ToSampleProvider();

        var totalSamplesApprox = (long)(reader.TotalTime.TotalSeconds * sampleProvider.WaveFormat.SampleRate * sampleProvider.WaveFormat.Channels);
        if (totalSamplesApprox <= 0) return Array.Empty<float>();

        var samplesPerPoint = Math.Max(1, totalSamplesApprox / totalPoints);
        var peaks = new float[totalPoints];

        var buffer = new float[4096];
        long currentSampleCount = 0;
        int currentPointIndex = 0;
        float currentMax = 0f;

        int read;
        while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < read; i++)
            {
                var absVal = Math.Abs(buffer[i]);
                if (absVal > currentMax) currentMax = absVal;

                currentSampleCount++;
                if (currentSampleCount >= samplesPerPoint && currentPointIndex < totalPoints)
                {
                    peaks[currentPointIndex++] = Math.Min(1.0f, currentMax);
                    currentMax = 0f;
                    currentSampleCount = 0;
                }
            }
        }

        while (currentPointIndex < totalPoints)
        {
            peaks[currentPointIndex++] = currentMax;
        }

        return peaks;
    }
}
