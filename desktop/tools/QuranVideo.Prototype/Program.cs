using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using QuranVideo.Prototype.Audio;
using QuranVideo.Prototype.Geometry;
using QuranVideo.Prototype.Image;
using QuranVideo.Prototype.Video;

Console.WriteLine("=============================================================");
Console.WriteLine("   QURAN VIDEO STUDIO - LOT 1 FEASIBILITY PROTOTYPE (.NET 9)  ");
Console.WriteLine("=============================================================");

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
var mushafRoot = Path.Combine(repoRoot, "data/mushaf/editions/marw-newMushaf/20260913-full");
var audioRoot = Path.Combine(repoRoot, "data/audio/sources");
var outputDir = Path.Combine(repoRoot, "desktop/doc/validation/prototype_output");
Directory.CreateDirectory(outputDir);

// Load opening reviews
var openingsJsonPath = Path.Combine(repoRoot, "src/Quran.Infrastructure/Data/marw-opening-review.json");
using var openingsDoc = JsonDocument.Parse(File.ReadAllText(openingsJsonPath));
var openingEntries = openingsDoc.RootElement.GetProperty("Entries").EnumerateArray()
    .ToDictionary(e => e.GetProperty("Surah").GetInt32(), e => (
        Page: e.GetProperty("Page").GetInt32(),
        LineCount: e.GetProperty("LineCount").GetInt32(),
        FirstTextLine: e.GetProperty("FirstTextLine").GetInt32()
    ));

Console.WriteLine($"[1] Loaded {openingEntries.Count} opening reviews.");

// Cases to prototype
var testCases = new (int Surah, int Ayah, string Name, string AudioPath, double StartSec, double EndSec)[]
{
    (1, 1, "01_Fatiha_1_1", Path.Combine(audioRoot, "001/original.mp3"), 0.0, 5.8),
    (1, 7, "02_Fatiha_1_7", Path.Combine(audioRoot, "001/original.mp3"), 50.0, 67.0),
    (2, 1, "03_Baqara_2_1", Path.Combine(audioRoot, "002/original.mp3"), 0.0, 15.0),
    (2, 2, "04_Baqara_2_2", Path.Combine(audioRoot, "002/original.mp3"), 15.0, 27.5),
    (2, 4, "05_Baqara_2_4", Path.Combine(audioRoot, "002/original.mp3"), 42.0, 58.0),
    (2, 255, "06_Baqara_2_255_Kursi", Path.Combine(audioRoot, "002/original.mp3"), 9000.0, 9080.0),
    (2, 282, "07_Baqara_2_282_Dayn", Path.Combine(audioRoot, "002/original.mp3"), 11000.0, 11200.0),
    (112, 1, "08_Ikhlas_112_1", Path.Combine(audioRoot, "112/original.mp3"), 0.0, 7.5),
    (112, 4, "09_Ikhlas_112_4", Path.Combine(audioRoot, "112/original.mp3"), 22.0, 32.0)
};

var imageResults = new List<object>();

foreach (var tc in testCases)
{
    Console.WriteLine($"\n--> Processing Ayah {tc.Surah}:{tc.Ayah} ({tc.Name})...");

    // Find page and row
    int pageNum = 0;
    JsonElement? rowMatch = null;
    JsonElement? cropBoxEl = null;
    JsonElement? textBoxEl = null;

    for (int p = 1; p <= 604; p++)
    {
        var pageJsonPath = Path.Combine(mushafRoot, $"page-{p:D3}.json");
        using var pageDoc = JsonDocument.Parse(File.ReadAllText(pageJsonPath));
        foreach (var r in pageDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            if (r.GetProperty("surah").GetInt32() == tc.Surah && r.GetProperty("ayah").GetInt32() == tc.Ayah)
            {
                pageNum = p;
                rowMatch = r.Clone();
                cropBoxEl = pageDoc.RootElement.GetProperty("boxes").GetProperty("cropBox").Clone();
                textBoxEl = pageDoc.RootElement.GetProperty("boxes").GetProperty("textBox").Clone();
                break;
            }
        }
        if (pageNum > 0) break;
    }

    if (pageNum == 0 || rowMatch == null)
    {
        Console.WriteLine($"[ERROR] Could not find Ayah {tc.Surah}:{tc.Ayah}");
        continue;
    }

    var imagePath = Path.Combine(mushafRoot, $"page-{pageNum:D3}.png");
    var crop = new NormalizedBox(
        cropBoxEl!.Value.GetProperty("left").GetDouble(),
        cropBoxEl!.Value.GetProperty("top").GetDouble(),
        cropBoxEl!.Value.GetProperty("width").GetDouble(),
        cropBoxEl!.Value.GetProperty("height").GetDouble());

    var text = new NormalizedBox(
        textBoxEl!.Value.GetProperty("left").GetDouble(),
        textBoxEl!.Value.GetProperty("top").GetDouble(),
        textBoxEl!.Value.GetProperty("width").GetDouble(),
        textBoxEl!.Value.GetProperty("height").GetDouble());

    // Calculate minimumTop if ayah 1
    double minTopPercent = 0.0;
    if (tc.Ayah == 1 && openingEntries.TryGetValue(tc.Surah, out var op))
    {
        minTopPercent = op.FirstTextLine * 100.0 / op.LineCount;
        Console.WriteLine($"    Opening clip applied: FirstTextLine={op.FirstTextLine}/{op.LineCount} ({minTopPercent:F1}%)");
    }

    var pixelRects = new List<PixelRect>();
    foreach (var rectEl in rowMatch.Value.GetProperty("rects").EnumerateArray())
    {
        var nb = new NormalizedBox(
            rectEl.GetProperty("left").GetDouble(),
            rectEl.GetProperty("top").GetDouble(),
            rectEl.GetProperty("width").GetDouble(),
            rectEl.GetProperty("height").GetDouble());

        var pr = AyahGeometry.ConvertToPixels(crop, text, nb, 1241, 1754, minTopPercent);
        if (pr.Width > 0 && pr.Height > 0)
        {
            pixelRects.Add(pr);
        }
    }

    var bbox = AyahGeometry.ComputeEnclosingBoundingBox(pixelRects, padding: 8);
    Console.WriteLine($"    Extracted {pixelRects.Count} rects. Enclosing bbox: X={bbox.RoundX}, Y={bbox.RoundY}, W={bbox.RoundWidth}, H={bbox.RoundHeight}");

    // Generate isolated image
    var isolatedPath = Path.Combine(outputDir, $"{tc.Name}_isolated.png");
    using (var isolatedBmp = AyahImageExtractor.ExtractIsolatedAyah(imagePath, pixelRects, bbox, Color.White))
    {
        isolatedBmp.Save(isolatedPath, ImageFormat.Png);
    }

    // Generate side-by-side comparison
    var comparisonPath = Path.Combine(outputDir, $"{tc.Name}_comparison.png");
    using (var compBmp = AyahImageExtractor.GenerateSideBySideComparison(imagePath, pixelRects, bbox))
    {
        compBmp.Save(comparisonPath, ImageFormat.Png);
    }

    imageResults.Add(new
    {
        Case = tc.Name,
        Surah = tc.Surah,
        Ayah = tc.Ayah,
        Page = pageNum,
        RectCount = pixelRects.Count,
        BBox = new { bbox.RoundX, bbox.RoundY, bbox.RoundWidth, bbox.RoundHeight },
        IsolatedImage = Path.GetFileName(isolatedPath),
        ComparisonImage = Path.GetFileName(comparisonPath)
    });
}

// 2. Audio Precision Benchmark
Console.WriteLine("\n[2] Running Audio Precision Benchmark...");
var audioResults = new List<AudioBenchmarkResult>();

var audioFilesToBenchmark = new[]
{
    Path.Combine(audioRoot, "001/original.mp3"),
    Path.Combine(audioRoot, "112/original.mp3"),
    Path.Combine(audioRoot, "002/original.mp3")
};

foreach (var af in audioFilesToBenchmark)
{
    if (!File.Exists(af)) continue;
    Console.WriteLine($"--> Benchmarking {Path.GetFileName(Path.GetDirectoryName(af))}/{Path.GetFileName(af)}...");
    var bench = AudioPrecisionBenchmark.BenchmarkFile(af, testSeekSec: 120.0);
    audioResults.Add(bench);
    Console.WriteLine($"    Size: {bench.FileSizeBytes:N0} bytes | FFprobe: {bench.FFprobeDurationSec:F2}s | NAudio: {bench.NAudioDurationSec:F2}s | Discrepancy: {bench.SeekDiscrepancyMs:F1}ms");
    Console.WriteLine($"    Decision: {bench.EvaluationDecision}");
}

// 3. Generate Prototype Videos
Console.WriteLine("\n[3] Generating Prototype MP4 Videos...");

// Video 1: Horizontal (1920x1080) for Fatiha 1:1
var fatiha1Isolated = Path.Combine(outputDir, "01_Fatiha_1_1_isolated.png");
var fatiha1Audio = Path.Combine(audioRoot, "001/original.mp3");
var videoHorizontalPath = Path.Combine(outputDir, "prototype_fatiha_1_1_horizontal.mp4");

Console.WriteLine("--> Generating Horizontal 1080p MP4 (Fatiha 1:1)...");
var reqH = new VideoGenerationRequest(
    fatiha1Isolated,
    fatiha1Audio,
    0.0,
    5.8,
    videoHorizontalPath,
    VideoOrientation.Horizontal);

var resH = await FfmpegVideoGenerator.GenerateAyahVideoAsync(reqH);
Console.WriteLine($"    Horizontal Video Result: Success={resH.Success}, Dimensions={resH.Width}x{resH.Height}, Duration={resH.DurationSeconds:F2}s, VCodec={resH.VideoCodec}, ACodec={resH.AudioCodec}, Size={resH.FileSizeBytes:N0} bytes");

// Video 2: Vertical (1080x1920) for Ikhlas 112:1
var ikhlas1Isolated = Path.Combine(outputDir, "08_Ikhlas_112_1_isolated.png");
var ikhlas1Audio = Path.Combine(audioRoot, "112/original.mp3");
var videoVerticalPath = Path.Combine(outputDir, "prototype_ikhlas_112_1_vertical.mp4");

Console.WriteLine("--> Generating Vertical 1080x1920 MP4 (Ikhlas 112:1)...");
var reqV = new VideoGenerationRequest(
    ikhlas1Isolated,
    ikhlas1Audio,
    0.0,
    7.5,
    videoVerticalPath,
    VideoOrientation.Vertical);

var resV = await FfmpegVideoGenerator.GenerateAyahVideoAsync(reqV);
Console.WriteLine($"    Vertical Video Result: Success={resV.Success}, Dimensions={resV.Width}x{resV.Height}, Duration={resV.DurationSeconds:F2}s, VCodec={resV.VideoCodec}, ACodec={resV.AudioCodec}, Size={resV.FileSizeBytes:N0} bytes");

// Save benchmark & execution report
var fullReport = new
{
    ExecutionDate = DateTime.UtcNow.ToString("O"),
    ImagesGenerated = imageResults,
    AudioBenchmark = audioResults,
    HorizontalVideo = resH,
    VerticalVideo = resV
};

var reportJsonPath = Path.Combine(outputDir, "lot1_prototype_results.json");
File.WriteAllText(reportJsonPath, JsonSerializer.Serialize(fullReport, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"\n[SUCCESS] Lot 1 Prototype completed! Results saved to {reportJsonPath}");
