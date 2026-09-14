using Microsoft.Extensions.Logging.Abstractions;
using Quran.AudioProcessor.Services;

namespace Quran.UnitTests;

public class AudioProcessorTests
{
    private readonly string _sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "audio", "sources", "001", "original.mp3");

    [Fact]
    public void ComputeChecksum_ReturnsValidSha256()
    {
        if (!File.Exists(_sourcePath)) return; // Skip if source not yet downloaded

        var processor = new FfmpegAudioProcessor(NullLogger<FfmpegAudioProcessor>.Instance);
        var checksum = processor.ComputeChecksum(_sourcePath);

        Assert.NotNull(checksum);
        Assert.Equal(64, checksum.Length); // SHA-256 hex string has 64 characters
    }

    [Fact]
    public async Task ProbeAudioAsync_ReturnsValidMetadata()
    {
        if (!File.Exists(_sourcePath)) return;

        var processor = new FfmpegAudioProcessor(NullLogger<FfmpegAudioProcessor>.Instance);
        var probe = await processor.ProbeAudioAsync(_sourcePath);

        Assert.True(probe.Duration.TotalSeconds > 60);
        Assert.Equal(1129009, probe.SizeBytes);
        Assert.Equal("audio/mpeg", probe.MimeType);
    }

    [Fact]
    public async Task ExtractSegmentAsync_CreatesValidMp3File()
    {
        if (!File.Exists(_sourcePath)) return;

        var tempOutput = Path.Combine(Path.GetTempPath(), $"test_fatiha_ayah1_{Guid.NewGuid():N}.mp3");
        try
        {
            var processor = new FfmpegAudioProcessor(NullLogger<FfmpegAudioProcessor>.Instance);
            
            // Extract Ayah 1: 0.6s to 6.4s (5.8s)
            var resultPath = await processor.ExtractSegmentAsync(
                _sourcePath,
                tempOutput,
                TimeSpan.FromSeconds(0.6),
                TimeSpan.FromSeconds(5.8)
            );

            Assert.True(File.Exists(resultPath));
            var probe = await processor.ProbeAudioAsync(resultPath);
            Assert.True(probe.Duration.TotalSeconds >= 5.0 && probe.Duration.TotalSeconds <= 6.5);
            Assert.True(probe.SizeBytes > 0);
        }
        finally
        {
            if (File.Exists(tempOutput))
            {
                File.Delete(tempOutput);
            }
        }
    }
}
