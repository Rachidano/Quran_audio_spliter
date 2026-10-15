using System.Diagnostics;
using System.IO;
using System.Text.Json;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

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
    VideoOrientation Orientation,
    CardCompositionSettings? Settings = null);

public sealed record MultiAyahTimelineEntry(
    int AyahNumber,
    string ImagePath,
    double StartTimeSec,
    double EndTimeSec)
{
    public double DurationSec => Math.Max(0, EndTimeSec - StartTimeSec);
}

public sealed record MultiAyahVideoRequest(
    string AudioSourcePath,
    IReadOnlyList<MultiAyahTimelineEntry> Timeline,
    string OutputVideoPath,
    VideoOrientation Orientation,
    CardCompositionSettings? Settings = null);

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
    public static async Task<VideoVerificationResult> GenerateAyahVideoAsync(
        VideoGenerationRequest req,
        CancellationToken cancellationToken = default)
    {
        var duration = req.EndTimeSec - req.StartTimeSec;
        if (duration <= 0.05)
        {
            return new VideoVerificationResult(false, "Audio duration too short (<50ms)", 0, 0, 0, null, null, 0);
        }

        var (targetWidth, targetHeight) = req.Orientation == VideoOrientation.Horizontal
            ? (1920, 1080)
            : (1080, 1920);

        var tempDir = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(req.OutputVideoPath)) ?? ".",
            $"tmp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var tempOutput = Path.Combine(tempDir, "output.mp4");
        string? tempComposedFrame = null;

        try
        {
            string inputImagePath = req.ImagePath;
            string videoFilter;

            bool hasEffectiveWatermark = req.Settings != null && (
                req.Settings.WatermarkType != WatermarkType.None ||
                !string.IsNullOrWhiteSpace(req.Settings.WatermarkText) ||
                (!string.IsNullOrWhiteSpace(req.Settings.WatermarkImagePath) && File.Exists(req.Settings.WatermarkImagePath)));

            if (req.Settings != null && (req.Settings.BackgroundType != BackgroundType.Light || hasEffectiveWatermark))
            {
                var settings = req.Settings with { TargetWidth = targetWidth, TargetHeight = targetHeight };
                tempComposedFrame = Path.Combine(tempDir, "composed_frame.png");
                AyahCardComposer.ComposeCardToFile(req.ImagePath, settings, tempComposedFrame);
                inputImagePath = tempComposedFrame;
                videoFilter = $"scale={targetWidth}:{targetHeight}";
            }
            else
            {
                var maxInnerW = (int)(targetWidth * 0.88);
                var maxInnerH = (int)(targetHeight * 0.88);
                maxInnerW -= maxInnerW % 2;
                maxInnerH -= maxInnerH % 2;

                videoFilter = $"scale=w={maxInnerW}:h={maxInnerH}:force_original_aspect_ratio=decrease," +
                              $"pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:color=white";
            }

            var args = new List<string>
            {
                "-y",
                "-ss", req.StartTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                "-t", duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                "-i", req.AudioPath,
                "-loop", "1",
                "-i", inputImagePath,
                "-vf", videoFilter,
                "-c:v", "libx264",
                "-preset", "fast",
                "-tune", "stillimage",
                "-pix_fmt", "yuv420p",
                "-r", "30",
                "-c:a", "aac",
                "-b:a", "192k",
                "-ar", "48000",
                "-shortest",
                "-movflags", "+faststart",
                tempOutput
            };

            var (success, _, error) = await RunFfmpegAsync(args, cancellationToken);
            if (!success || !File.Exists(tempOutput))
            {
                return new VideoVerificationResult(false, $"FFmpeg failed: {error}", 0, 0, 0, null, null, 0);
            }

            var verification = await ProbeVideoAsync(tempOutput, cancellationToken);
            if (!verification.Success)
            {
                return verification;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(req.OutputVideoPath)) ?? ".");
            if (File.Exists(req.OutputVideoPath)) File.Delete(req.OutputVideoPath);
            File.Move(tempOutput, req.OutputVideoPath);

            return verification;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    public static async Task<VideoVerificationResult> GenerateMultiAyahVideoAsync(
        MultiAyahVideoRequest req,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (req.Timeline == null || req.Timeline.Count == 0)
        {
            return new VideoVerificationResult(false, "قائمة الآيات فارغة.", 0, 0, 0, null, null, 0);
        }

        if (!File.Exists(req.AudioSourcePath))
        {
            return new VideoVerificationResult(false, $"ملف الصوت المصدر غير موجود: {req.AudioSourcePath}", 0, 0, 0, null, null, 0);
        }

        foreach (var entry in req.Timeline)
        {
            if (entry.DurationSec <= 0.05)
            {
                return new VideoVerificationResult(false, $"مدة الآية {entry.AyahNumber} قصيرة جداً (أقل من 50 مللي ثانية).", 0, 0, 0, null, null, 0);
            }
            if (!File.Exists(entry.ImagePath))
            {
                return new VideoVerificationResult(false, $"صورة الآية {entry.AyahNumber} غير موجودة: {entry.ImagePath}", 0, 0, 0, null, null, 0);
            }
        }

        var (targetWidth, targetHeight) = req.Orientation == VideoOrientation.Horizontal
            ? (1920, 1080)
            : (1080, 1920);

        var settings = (req.Settings ?? CardCompositionSettings.CreateDefault(req.Orientation)) with
        {
            TargetWidth = targetWidth,
            TargetHeight = targetHeight
        };

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(req.OutputVideoPath)) ?? ".";
        Directory.CreateDirectory(outputDir);

        var tempDir = Path.Combine(outputDir, $"tmp_multi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        progress?.Report(10);

        try
        {
            // 1. Compose each ayah frame
            var framePaths = new List<string>();
            for (int i = 0; i < req.Timeline.Count; i++)
            {
                var entry = req.Timeline[i];
                var framePath = Path.Combine(tempDir, $"frame_{i:D3}.png");
                AyahCardComposer.ComposeCardToFile(entry.ImagePath, settings, framePath);
                framePaths.Add(framePath);
            }

            progress?.Report(25);

            // 2. Check audio continuity
            bool isContinuous = true;
            for (int i = 1; i < req.Timeline.Count; i++)
            {
                if (Math.Abs(req.Timeline[i].StartTimeSec - req.Timeline[i - 1].EndTimeSec) > 0.05)
                {
                    isContinuous = false;
                    break;
                }
            }

            var tempFinal = Path.Combine(tempDir, "final.mp4");
            double expectedDuration = req.Timeline.Sum(t => t.DurationSec);

            if (isContinuous)
            {
                // Continuous Audio Mode: Extract continuous audio once, render video parts, concat video, mux!
                var audioStart = req.Timeline[0].StartTimeSec;
                var tempAudio = Path.Combine(tempDir, "continuous_audio.m4a");

                var audioArgs = new List<string>
                {
                    "-y",
                    "-ss", audioStart.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    "-t", expectedDuration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    "-i", req.AudioSourcePath,
                    "-vn",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-ar", "48000",
                    tempAudio
                };

                var (aOk, _, aErr) = await RunFfmpegAsync(audioArgs, cancellationToken);
                if (!aOk || !File.Exists(tempAudio))
                {
                    return new VideoVerificationResult(false, $"FFmpeg continuous audio extract failed: {aErr}", 0, 0, 0, null, null, 0);
                }

                progress?.Report(35);

                // Render video parts
                var videoParts = new List<string>();
                for (int i = 0; i < req.Timeline.Count; i++)
                {
                    var entry = req.Timeline[i];
                    var partVideo = Path.Combine(tempDir, $"vpart_{i:D3}.mp4");

                    var partArgs = new List<string>
                    {
                        "-y",
                        "-loop", "1",
                        "-t", entry.DurationSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        "-i", framePaths[i],
                        "-c:v", "libx264",
                        "-preset", "fast",
                        "-tune", "stillimage",
                        "-pix_fmt", "yuv420p",
                        "-r", "30",
                        partVideo
                    };

                    var (vOk, _, vErr) = await RunFfmpegAsync(partArgs, cancellationToken);
                    if (!vOk || !File.Exists(partVideo))
                    {
                        return new VideoVerificationResult(false, $"FFmpeg video part {i} failed: {vErr}", 0, 0, 0, null, null, 0);
                    }

                    videoParts.Add(partVideo);
                    int pct = 35 + (int)(35.0 * (i + 1) / req.Timeline.Count);
                    progress?.Report(pct);
                }

                // Concat video parts
                var concatListPath = Path.Combine(tempDir, "video_list.txt");
                var concatLines = videoParts.Select(p => $"file '{Path.GetFileName(p)}'");
                await File.WriteAllLinesAsync(concatListPath, concatLines, cancellationToken);

                var tempMergedVideo = Path.Combine(tempDir, "merged_video.mp4");
                var concatArgs = new List<string>
                {
                    "-y",
                    "-f", "concat",
                    "-safe", "0",
                    "-i", concatListPath,
                    "-c", "copy",
                    tempMergedVideo
                };

                var (mOk, _, mErr) = await RunFfmpegAsync(concatArgs, cancellationToken);
                if (!mOk || !File.Exists(tempMergedVideo))
                {
                    return new VideoVerificationResult(false, $"FFmpeg video concat failed: {mErr}", 0, 0, 0, null, null, 0);
                }

                progress?.Report(80);

                // Mux merged video + continuous audio
                var muxArgs = new List<string>
                {
                    "-y",
                    "-i", tempMergedVideo,
                    "-i", tempAudio,
                    "-c:v", "copy",
                    "-c:a", "copy",
                    "-map", "0:v:0",
                    "-map", "1:a:0",
                    "-shortest",
                    "-movflags", "+faststart",
                    tempFinal
                };

                var (muxOk, _, muxErr) = await RunFfmpegAsync(muxArgs, cancellationToken);
                if (!muxOk || !File.Exists(tempFinal))
                {
                    return new VideoVerificationResult(false, $"FFmpeg final mux failed: {muxErr}", 0, 0, 0, null, null, 0);
                }
            }
            else
            {
                // Non-continuous mode (with gaps / separate takes)
                var partVideos = new List<string>();
                for (int i = 0; i < req.Timeline.Count; i++)
                {
                    var entry = req.Timeline[i];
                    var partVideo = Path.Combine(tempDir, $"part_{i:D3}.mp4");

                    var partArgs = new List<string>
                    {
                        "-y",
                        "-ss", entry.StartTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        "-t", entry.DurationSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        "-i", req.AudioSourcePath,
                        "-loop", "1",
                        "-t", entry.DurationSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        "-i", framePaths[i],
                        "-map", "1:v:0",
                        "-map", "0:a:0",
                        "-c:v", "libx264",
                        "-preset", "fast",
                        "-tune", "stillimage",
                        "-pix_fmt", "yuv420p",
                        "-r", "30",
                        "-c:a", "aac",
                        "-b:a", "192k",
                        "-ar", "48000",
                        "-shortest",
                        partVideo
                    };

                    var (pOk, _, pErr) = await RunFfmpegAsync(partArgs, cancellationToken);
                    if (!pOk || !File.Exists(partVideo))
                    {
                        return new VideoVerificationResult(false, $"FFmpeg part {i} failed: {pErr}", 0, 0, 0, null, null, 0);
                    }

                    partVideos.Add(partVideo);
                    int pct = 25 + (int)(50.0 * (i + 1) / req.Timeline.Count);
                    progress?.Report(pct);
                }

                var concatListPath = Path.Combine(tempDir, "list.txt");
                var concatLines = partVideos.Select(p => $"file '{Path.GetFileName(p)}'");
                await File.WriteAllLinesAsync(concatListPath, concatLines, cancellationToken);

                var concatArgs = new List<string>
                {
                    "-y",
                    "-f", "concat",
                    "-safe", "0",
                    "-i", concatListPath,
                    "-c:v", "copy",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-movflags", "+faststart",
                    tempFinal
                };

                var (cOk, _, cErr) = await RunFfmpegAsync(concatArgs, cancellationToken);
                if (!cOk || !File.Exists(tempFinal))
                {
                    return new VideoVerificationResult(false, $"FFmpeg concat failed: {cErr}", 0, 0, 0, null, null, 0);
                }
            }

            progress?.Report(90);

            // 3. Verification & probe
            var verification = await ProbeVideoAsync(tempFinal, cancellationToken);
            if (!verification.Success)
            {
                return verification;
            }

            // Tolérance de durée adaptée : paquets AAC de ~21ms + cadence 30fps (~33ms par trame)
            // Pour les séquences multi-ayat, la tolérance s'adapte au nombre de segments concaténés pour éviter tout rejet injustifié
            var tolerance = Math.Max(0.25, (req.Timeline?.Count ?? 1) * 0.04);
            if (Math.Abs(verification.DurationSeconds - expectedDuration) > tolerance)
            {
                return new VideoVerificationResult(
                    false,
                    $"المدة الناتجة ({verification.DurationSeconds:F2}s) تختلف عن المتوقع ({expectedDuration:F2}s) بأكثر من التسامح المسموح ({tolerance:F2}s).",
                    verification.Width,
                    verification.Height,
                    verification.DurationSeconds,
                    verification.VideoCodec,
                    verification.AudioCodec,
                    verification.FileSizeBytes);
            }

            // 4. Atomic publication
            if (File.Exists(req.OutputVideoPath)) File.Delete(req.OutputVideoPath);
            File.Move(tempFinal, req.OutputVideoPath);

            progress?.Report(100);
            return verification;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    public static async Task<(bool Success, string? ErrorMessage)> ExtractAyahAudioAsync(
        string sourceAudioPath,
        double startTimeSec,
        double endTimeSec,
        string outputMp3Path,
        CancellationToken cancellationToken = default)
    {
        var duration = endTimeSec - startTimeSec;
        if (duration <= 0.05)
        {
            return (false, "مدة المقطع الصوتي قصيرة جداً (أقل من 50 مللي ثانية).");
        }

        if (!File.Exists(sourceAudioPath))
        {
            return (false, "ملف الصوت المصدر غير موجود.");
        }

        var tempOutput = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputMp3Path)) ?? ".",
            $"tmp_audio_{Guid.NewGuid():N}.mp3");

        var args = new List<string>
        {
            "-y",
            "-ss", startTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            "-to", endTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            "-i", sourceAudioPath,
            "-vn",
            "-c:a", "libmp3lame",
            "-q:a", "2",
            tempOutput
        };

        var (success, _, error) = await RunFfmpegAsync(args, cancellationToken);
        if (!success || !File.Exists(tempOutput) || new FileInfo(tempOutput).Length == 0)
        {
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
            return (false, $"FFmpeg audio error: {error}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMp3Path)) ?? ".");
        if (File.Exists(outputMp3Path)) File.Delete(outputMp3Path);
        File.Move(tempOutput, outputMp3Path);

        return (true, null);
    }

    public static async Task<VideoVerificationResult> ProbeVideoAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var probe = await ExternalProcessRunner.RunAsync(
            GetFfprobePath(),
            new[] { "-v", "quiet", "-print_format", "json", "-show_format", "-show_streams", videoPath },
            ExternalProcessRunner.FfprobeInactivityTimeout,
            cancellationToken);
        var stdout = probe.StandardOutput;

        if (!probe.Success)
        {
            return new VideoVerificationResult(false, probe.TimedOut ? probe.StandardError : "FFprobe failed", 0, 0, 0, null, null, 0);
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

    private static async Task<(bool Success, string Output, string Error)> RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var run = await ExternalProcessRunner.RunAsync(
            GetFfmpegPath(),
            arguments,
            ExternalProcessRunner.FfmpegInactivityTimeout,
            cancellationToken);
        return (run.Success, run.StandardOutput, run.StandardError);
    }

    public static string GetFfmpegPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localCandidate = Path.Combine(appDir, "tools", "ffmpeg.exe");
        if (File.Exists(localCandidate)) return localCandidate;
        var directCandidate = Path.Combine(appDir, "ffmpeg.exe");
        if (File.Exists(directCandidate)) return directCandidate;
        return "ffmpeg";
    }

    public static string GetFfprobePath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localCandidate = Path.Combine(appDir, "tools", "ffprobe.exe");
        if (File.Exists(localCandidate)) return localCandidate;
        var directCandidate = Path.Combine(appDir, "ffprobe.exe");
        if (File.Exists(directCandidate)) return directCandidate;
        return "ffprobe";
    }
}
