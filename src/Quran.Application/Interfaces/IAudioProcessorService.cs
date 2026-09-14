namespace Quran.Application.Interfaces;

public record AudioProbeResult(
    TimeSpan Duration,
    long SizeBytes,
    int Bitrate,
    string MimeType,
    string Checksum
);

public interface IAudioProcessorService
{
    Task<AudioProbeResult> ProbeAudioAsync(string filePath, CancellationToken cancellationToken = default);
    Task<string> ExtractSegmentAsync(string sourcePath, string outputPath, TimeSpan startTime, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeSpan>> DetectSilencesAsync(string filePath, double noiseDb = -30, double minDurationSeconds = 0.5, CancellationToken cancellationToken = default);
    string ComputeChecksum(string filePath);
}
