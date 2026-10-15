using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;

namespace QuranVideo.Desktop.Services;

public sealed class AyahImageService
{
    private readonly WorkspaceService _workspaceService;

    public AyahImageService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    private string GetDbPath() => Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");

    private AppDbContext CreateDbContext() => new(GetDbPath());

    public string ResolvePageImagePath(int pageNumber)
    {
        var localPath = Path.Combine(_workspaceService.WorkspacePath, "media", "mushaf", "pages", $"page-{pageNumber:D3}.png");
        if (File.Exists(localPath)) return localPath;

        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pack", $"page-{pageNumber:D3}.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mushaf", $"page-{pageNumber:D3}.png"),
            Path.Combine(_workspaceService.WorkspacePath, "pack", $"page-{pageNumber:D3}.png"),
            Path.GetFullPath($"data/mushaf/editions/marw-newMushaf/20260913-full/page-{pageNumber:D3}.png"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\data\mushaf\editions\marw-newMushaf\20260913-full", $"page-{pageNumber:D3}.png"))
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                try
                {
                    var destDir = Path.Combine(_workspaceService.WorkspacePath, "media", "mushaf", "pages");
                    Directory.CreateDirectory(destDir);
                    var destFile = Path.Combine(destDir, $"page-{pageNumber:D3}.png");
                    if (!File.Exists(destFile))
                    {
                        File.Copy(c, destFile, true);
                    }
                    return destFile;
                }
                catch
                {
                    return c;
                }
            }
        }

        return string.Empty;
    }

    public async Task<AyahImageRecipeEntity?> GetRecipeAsync(string ayahId)
    {
        using var db = CreateDbContext();
        return await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
    }

    public async Task<(bool Success, string ImagePath, AyahImageRecipeEntity Recipe)> GetOrExtractAyahImageAsync(
        int surahNumber,
        int ayahNumber,
        bool forceRecompute = false)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        var imagesDir = Path.Combine(_workspaceService.WorkspacePath, "media", "images", "ayahs");
        Directory.CreateDirectory(imagesDir);

        using var db = CreateDbContext();
        var recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
        if (recipe == null)
        {
            var surahInfo = SurahCatalogService.GetSurah(surahNumber);
            var pageNo = surahInfo?.StartPage ?? 1;

            var ayahEntity = await db.Ayahs.FirstOrDefaultAsync(a => a.Id == ayahId);
            if (ayahEntity != null) pageNo = ayahEntity.PageNumber;

            recipe = new AyahImageRecipeEntity
            {
                AyahId = ayahId,
                SurahNumber = surahNumber,
                AyahNumber = ayahNumber,
                PageNumber = pageNo,
                Status = AyahImageStatus.Suggested,
                Revision = 1
            };
            db.ImageRecipes.Add(recipe);
            await db.SaveChangesAsync();
        }

        var fileName = $"ayah_{surahNumber:D3}_{ayahNumber:D3}_rev{recipe.Revision}.png";
        var finalPath = Path.Combine(imagesDir, fileName);

        // Check if valid cache exists on disk
        if (!forceRecompute && File.Exists(finalPath) && !string.IsNullOrEmpty(recipe.ImageSha256))
        {
            return (true, finalPath, recipe);
        }

        // Locate page image
        var pageImagePath = ResolvePageImagePath(recipe.PageNumber);
        if (string.IsNullOrEmpty(pageImagePath) || !File.Exists(pageImagePath))
        {
            return (false, string.Empty, recipe);
        }

        // Load regions
        var regionEntities = await db.Regions
            .Where(r => r.AyahId == ayahId)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync();

        if (regionEntities.Count == 0)
        {
            LoggingService.Instance.LogWarning($"Aucune région trouvée en base pour l'ayah {ayahId} (page {recipe.PageNumber}) : extraction refusée.");
            return (false, string.Empty, recipe);
        }

        List<PixelRect> rects = new();
        foreach (var re in regionEntities)
        {
            rects.Add(new PixelRect(re.PixelX, re.PixelY, re.PixelWidth, re.PixelHeight));
        }

        // Apply recipe margin adjustments
        var adjustedBox = ComputeAdjustedBox(rects, recipe);
        var adjustedRects = ApplyMarginsToRects(rects, recipe);

        // Extract lossless PNG on white background (L07-01 & L07-06)
        using var isolatedBmp = AyahImageExtractor.ExtractIsolatedAyah(pageImagePath, adjustedRects, adjustedBox, Color.White);
        using (var ms = new MemoryStream())
        {
            isolatedBmp.Save(ms, ImageFormat.Png);
            File.WriteAllBytes(finalPath, ms.ToArray());
        }

        // Clean up older revision files for this ayah to prevent disk clutter
        try
        {
            var oldFiles = Directory.GetFiles(imagesDir, $"ayah_{surahNumber:D3}_{ayahNumber:D3}_rev*.png");
            foreach (var old in oldFiles)
            {
                if (!string.Equals(old, finalPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(old); } catch { }
                }
            }
        }
        catch { }

        // Calculate SHA-256 and dimensions
        recipe.RelativeImagePath = Path.GetRelativePath(_workspaceService.WorkspacePath, finalPath);
        recipe.Width = isolatedBmp.Width;
        recipe.Height = isolatedBmp.Height;
        recipe.ImageSha256 = ComputeSha256(finalPath);
        recipe.SourcePageSha256 = ComputeSha256(pageImagePath);
        recipe.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Write sidecar manifest JSON (L07-06)
        var manifestPath = Path.Combine(imagesDir, $"ayah_{surahNumber:D3}_{ayahNumber:D3}_manifest.json");
        var manifestObj = new ImageRecipeManifest
        {
            AyahId = ayahId,
            SurahNumber = surahNumber,
            AyahNumber = ayahNumber,
            PageNumber = recipe.PageNumber,
            Revision = recipe.Revision,
            Status = recipe.Status.ToString(),
            Width = recipe.Width,
            Height = recipe.Height,
            ImageSha256 = recipe.ImageSha256,
            SourcePageSha256 = recipe.SourcePageSha256,
            Margins = new MarginAdjustments(recipe.MarginTop, recipe.MarginBottom, recipe.MarginLeft, recipe.MarginRight),
            GeneratedAtUtc = DateTime.UtcNow
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true }));

        return (true, finalPath, recipe);
    }

    public async Task<bool> ApproveImageAsync(int surahNumber, int ayahNumber)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        using var db = CreateDbContext();
        var recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
        if (recipe == null)
        {
            var res = await GetOrExtractAyahImageAsync(surahNumber, ayahNumber);
            if (!res.Success) return false;
            recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
            if (recipe == null) return false;
        }

        recipe.Status = AyahImageStatus.Approved;
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string ImagePath, AyahImageRecipeEntity Recipe)> ApplyCorrectionAsync(
        int surahNumber,
        int ayahNumber,
        int deltaTop,
        int deltaBottom,
        int deltaLeft,
        int deltaRight)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        using var db = CreateDbContext();
        var recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
        if (recipe == null)
        {
            // La recette est créée par un autre contexte : la recharger ici pour que
            // les marges modifiées ci-dessous soient bien suivies et enregistrées.
            await GetOrExtractAyahImageAsync(surahNumber, ayahNumber);
            recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
            if (recipe == null) return (false, string.Empty, new AyahImageRecipeEntity { AyahId = ayahId });
        }

        recipe.MarginTop = Math.Clamp(recipe.MarginTop + deltaTop, -100, 200);
        recipe.MarginBottom = Math.Clamp(recipe.MarginBottom + deltaBottom, -100, 200);
        recipe.MarginLeft = Math.Clamp(recipe.MarginLeft + deltaLeft, -100, 200);
        recipe.MarginRight = Math.Clamp(recipe.MarginRight + deltaRight, -100, 200);
        recipe.Revision++; // L07-06: increment revision to invalidate dependent compositions
        recipe.Status = AyahImageStatus.Corrected;
        recipe.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return await GetOrExtractAyahImageAsync(surahNumber, ayahNumber, forceRecompute: true);
    }

    public async Task<(bool Success, string ImagePath, AyahImageRecipeEntity Recipe)> ResetCorrectionAsync(
        int surahNumber,
        int ayahNumber)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        using var db = CreateDbContext();
        var recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
        if (recipe != null)
        {
            recipe.MarginTop = 0;
            recipe.MarginBottom = 0;
            recipe.MarginLeft = 0;
            recipe.MarginRight = 0;
            recipe.Revision++;
            recipe.Status = AyahImageStatus.Suggested;
            recipe.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return await GetOrExtractAyahImageAsync(surahNumber, ayahNumber, forceRecompute: true);
    }

    public async Task<string> GenerateSideBySideComparisonAsync(int surahNumber, int ayahNumber)
    {
        var ayahId = $"{surahNumber}:{ayahNumber}";
        var compDir = Path.Combine(_workspaceService.WorkspacePath, "media", "images", "comparisons");
        Directory.CreateDirectory(compDir);

        using var db = CreateDbContext();
        var recipe = await db.ImageRecipes.FirstOrDefaultAsync(r => r.AyahId == ayahId);
        var pageNo = recipe?.PageNumber ?? 1;

        var pagePath = ResolvePageImagePath(pageNo);
        if (string.IsNullOrEmpty(pagePath) || !File.Exists(pagePath)) return string.Empty;

        var regionEntities = await db.Regions
            .Where(r => r.AyahId == ayahId)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync();

        List<PixelRect> rects = new();
        foreach (var re in regionEntities)
        {
            rects.Add(new PixelRect(re.PixelX, re.PixelY, re.PixelWidth, re.PixelHeight));
        }

        if (rects.Count == 0)
        {
            using var pageStream = new MemoryStream(File.ReadAllBytes(pagePath));
            using var bmp = new Bitmap(pageStream);
            rects.Add(new PixelRect(50, 100, bmp.Width - 100, 200));
        }

        // L'aperçu doit montrer exactement ce que produira l'extraction, marges comprises
        var box = AyahGeometry.ComputeEnclosingBoundingBox(rects);
        if (recipe != null && regionEntities.Count > 0)
        {
            box = ComputeAdjustedBox(rects, recipe);
            rects = ApplyMarginsToRects(rects, recipe);
        }
        using var compBmp = AyahImageExtractor.GenerateSideBySideComparison(pagePath, rects, box);

        var compPath = Path.Combine(compDir, $"comp_{surahNumber:D3}_{ayahNumber:D3}.png");
        using (var msComp = new MemoryStream())
        {
            compBmp.Save(msComp, ImageFormat.Png);
            File.WriteAllBytes(compPath, msComp.ToArray());
        }
        return compPath;
    }

    private static PixelRect ComputeAdjustedBox(IReadOnlyList<PixelRect> rects, AyahImageRecipeEntity recipe)
    {
        var boundingBox = AyahGeometry.ComputeEnclosingBoundingBox(rects);
        return new PixelRect(
            boundingBox.X - recipe.MarginLeft,
            boundingBox.Y - recipe.MarginTop,
            Math.Max(1, boundingBox.Width + recipe.MarginLeft + recipe.MarginRight),
            Math.Max(1, boundingBox.Height + recipe.MarginTop + recipe.MarginBottom));
    }

    /// <summary>
    /// Étend (ou réduit) les rectangles situés sur les bords extérieurs du bloc de l'ayah
    /// afin que les marges récupèrent les pixels coupés (signes, points). Une taille minimale
    /// de 1 px est conservée pour qu'une marge négative ne fasse jamais disparaître une ligne.
    /// </summary>
    private static List<PixelRect> ApplyMarginsToRects(IReadOnlyList<PixelRect> rects, AyahImageRecipeEntity recipe)
    {
        var adjustedRects = new List<PixelRect>();
        if (rects.Count == 0) return adjustedRects;

        var minY = rects.Min(r => r.RoundY);
        var maxY = rects.Max(r => r.RoundY + r.RoundHeight);
        var minX = rects.Min(r => r.RoundX);
        var maxX = rects.Max(r => r.RoundX + r.RoundWidth);

        foreach (var r in rects)
        {
            var x = r.X;
            var y = r.Y;
            var w = r.Width;
            var h = r.Height;

            // Expand top line(s) upward
            if (rects.Count == 1 || r.RoundY <= minY + 5)
            {
                y -= recipe.MarginTop;
                h += recipe.MarginTop;
            }
            // Expand bottom line(s) downward
            if (rects.Count == 1 || r.RoundY + r.RoundHeight >= maxY - 5)
            {
                h += recipe.MarginBottom;
            }
            // Expand left/right edges
            if (rects.Count == 1 || r.RoundX <= minX + 5)
            {
                x -= recipe.MarginLeft;
                w += recipe.MarginLeft;
            }
            if (rects.Count == 1 || r.RoundX + r.RoundWidth >= maxX - 5)
            {
                w += recipe.MarginRight;
            }

            adjustedRects.Add(new PixelRect(x, y, Math.Max(1, w), Math.Max(1, h)));
        }

        return adjustedRects;
    }

    private static string ComputeSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

public sealed class ImageRecipeManifest
{
    [JsonPropertyName("ayahId")]
    public string AyahId { get; set; } = string.Empty;

    [JsonPropertyName("surahNumber")]
    public int SurahNumber { get; set; }

    [JsonPropertyName("ayahNumber")]
    public int AyahNumber { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Suggested";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("imageSha256")]
    public string ImageSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sourcePageSha256")]
    public string SourcePageSha256 { get; set; } = string.Empty;

    [JsonPropertyName("margins")]
    public MarginAdjustments Margins { get; set; } = new(0, 0, 0, 0);

    [JsonPropertyName("generatedAtUtc")]
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public record MarginAdjustments(
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("bottom")] int Bottom,
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("right")] int Right);
