using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace QuranVideo.Prototype.Video;

public enum VideoOrientation
{
    Horizontal, // 1920 x 1080
    Vertical    // 1080 x 1920
}

public sealed record VideoGenerationRequest(
    string ImagePath,
    string AudioPath,
    double StartTimeSec,
    double EndTimeSec,
    string OutputVideoPath,
    VideoOrientation Orientation);

public sealed record VideoVerificationResult(
    bool Success,
    string? ErrorMessage,
    int Width,
    int Height,
    double DurationSeconds,
    string? VideoCodec,
    string? AudioCodec,
    long FileSizeBytes);

public static class FfmpegVideoGenerator
{
    public static async Task<VideoVerificationResult> GenerateAyahVideoAsync(VideoGenerationRequest req)
    {
        var duration = req.EndTimeSec - req.StartTimeSec;
        if (duration <= 0.05)
        {
            return new VideoVerificationResult(false, "Audio duration too short (<50ms)", 0, 0, 0, null, null, 0);
        }

        var (targetWidth, targetHeight) = req.Orientation == VideoOrientation.Horizontal
            ? (1920, 1080)
            : (1080, 1920);

        var tempOutput = Path.Combine(
            Path.GetDirectoryName(req.OutputVideoPath) ?? ".",
            $"tmp_{Guid.NewGuid():N}.mp4");

        // FFmpeg filter:
        // 1. Pad image to 16:9 or 9:16 proportionally on white canvas:
        //    scale to fit within targetWidth*0.85 and targetHeight*0.85 preserving aspect ratio
        //    then pad to targetWidth x targetHeight with white background (#ffffff)
        var maxInnerW = (int)(targetWidth * 0.88);
        var maxInnerH = (int)(targetHeight * 0.88);

        // Ensure even dimensions
        maxInnerW -= maxInnerW % 2;
        maxInnerH -= maxInnerH % 2;

        var videoFilter = $"scale=w={maxInnerW}:h={maxInnerH}:force_original_aspect_ratio=decrease," +
                          $"pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:color=white";

        var psi = new ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        // ffmpeg -y -ss <start> -t <duration> -i <audio> -loop 1 -i <image> -c:v libx264 -tune stillimage -pix_fmt yuv420p -r 30 -vf <filter> -c:a aac -b:a 192k -ar 48000 -shortest -movflags +faststart <output>
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(req.StartTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(req.AudioPath);
        psi.ArgumentList.Add("-loop");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(req.ImagePath);
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add(videoFilter);
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("fast");
        psi.ArgumentList.Add("-tune");
        psi.ArgumentList.Add("stillimage");
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("yuv420p");
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add("30");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("192k");
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("48000");
        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");
        psi.ArgumentList.Add(tempOutput);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stderr = await stderrTask;

        if (process.ExitCode != 0 || !File.Exists(tempOutput))
        {
            return new VideoVerificationResult(false, $"FFmpeg failed (code {process.ExitCode}): {stderr}", 0, 0, 0, null, null, 0);
        }

        // Verify with FFprobe
        var verification = await ProbeVideoAsync(tempOutput);
        if (!verification.Success)
        {
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
            return verification;
        }

        // Atomic move to final destination
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(req.OutputVideoPath)) ?? ".");
        if (File.Exists(req.OutputVideoPath)) File.Delete(req.OutputVideoPath);
        File.Move(tempOutput, req.OutputVideoPath);

        return verification;
    }

    public static async Task<VideoVerificationResult> ProbeVideoAsync(string videoPath)
    {
        var psi = new ProcessStartInfo("ffprobe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("quiet");
        psi.ArgumentList.Add("-print_format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("-show_format");
        psi.ArgumentList.Add("-show_streams");
        psi.ArgumentList.Add(videoPath);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            return new VideoVerificationResult(false, "FFprobe failed", 0, 0, 0, null, null, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var format = root.GetProperty("format");
            var duration = double.Parse(format.GetProperty("duration").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            var size = long.Parse(format.GetProperty("size").GetString() ?? "0");

            int width = 0, height = 0;
            string? vCodec = null, aCodec = null;

            foreach (var stream in root.GetProperty("streams").EnumerateArray())
            {
                var type = stream.GetProperty("codec_type").GetString();
                if (type == "video" && vCodec == null)
                {
                    vCodec = stream.GetProperty("codec_name").GetString();
                    width = stream.GetProperty("width").GetInt32();
                    height = stream.GetProperty("height").GetInt32();
                }
                else if (type == "audio" && aCodec == null)
                {
                    aCodec = stream.GetProperty("codec_name").GetString();
                }
            }

            if (vCodec == null || aCodec == null || width == 0 || height == 0)
            {
                return new VideoVerificationResult(false, "Missing audio or video stream", width, height, duration, vCodec, aCodec, size);
            }

            return new VideoVerificationResult(true, null, width, height, duration, vCodec, aCodec, size);
        }
        catch (Exception ex)
        {
            return new VideoVerificationResult(false, $"JSON probe parsing failed: {ex.Message}", 0, 0, 0, null, null, 0);
        }
    }
}
