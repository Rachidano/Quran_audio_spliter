using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;

namespace QuranVideo.Desktop.Services;

public sealed record ImportProgressReport(int CurrentPage, int TotalPages, string Message);

public sealed class CorpusImportService
{
    private readonly WorkspaceService _workspaceService;

    public CorpusImportService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    public async Task<bool> IsEditionImportedAsync(string editionCode, string revision)
    {
        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        if (!File.Exists(dbPath)) return false;

        using var db = new AppDbContext(dbPath);
        return await db.Editions.AnyAsync(e => e.Code == editionCode && e.Revision == revision);
    }

    public async Task<(bool Success, string? ErrorMessage)> ImportPackAsync(
        string editionSourceDir,
        string? qpcSourceJsonPath = null,
        string? openingReviewJsonPath = null,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(editionSourceDir))
        {
            return (false, $"مجلد المصحف غير موجود: {editionSourceDir}");
        }

        var manifestPath = Path.Combine(editionSourceDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return (false, "ملف manifest.json مفقود في مجلد المصحف.");
        }

        LoggingService.Instance.LogInfo($"Début de l'import du pack depuis {editionSourceDir}...");

        // Parse manifest
        using var manifestDoc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken));
        var root = manifestDoc.RootElement;
        var editionCode = root.TryGetProperty("edition", out var edProp) ? edProp.GetString() ?? "newMushaf" : "newMushaf";
        var revision = root.TryGetProperty("revision", out var revProp) ? revProp.GetString() ?? "20260913-full" : "20260913-full";
        var provenance = root.TryGetProperty("sourceUrl", out var pProp) ? pProp.GetString() ?? "" : "";

        int totalPages = 604;
        if (root.TryGetProperty("pageCount", out var pcProp))
        {
            totalPages = pcProp.GetInt32();
        }
        else if (root.TryGetProperty("totalPages", out var tpProp))
        {
            totalPages = tpProp.GetInt32();
        }

        // Map expected files sha256 if declared in manifest
        var expectedFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("files", out var filesArray) && filesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var fElem in filesArray.EnumerateArray())
            {
                if (fElem.TryGetProperty("file", out var fnProp) && fElem.TryGetProperty("sha256", out var hashProp))
                {
                    var fn = fnProp.GetString();
                    var h = hashProp.GetString();
                    if (!string.IsNullOrEmpty(fn) && !string.IsNullOrEmpty(h))
                    {
                        expectedFileHashes[fn] = h.ToLowerInvariant();
                    }
                }
            }
        }

        // Check if already imported
        var dbPath = Path.Combine(_workspaceService.WorkspacePath, "data", "quran.db");
        using var db = new AppDbContext(dbPath);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Editions.AnyAsync(e => e.Code == editionCode && e.Revision == revision, cancellationToken)
            && await db.Regions.AnyAsync(cancellationToken))
        {
            LoggingService.Instance.LogInfo($"Édition {editionCode} {revision} déjà importée dans la base SQLite.");
            return (true, null);
        }

        // Load opening clip rules if available
        var openingReviewClips = new Dictionary<int, double>(); // page -> minTopPercent
        if (!string.IsNullOrWhiteSpace(openingReviewJsonPath) && File.Exists(openingReviewJsonPath))
        {
            try
            {
                using var revDoc = JsonDocument.Parse(await File.ReadAllTextAsync(openingReviewJsonPath, cancellationToken));
                var revRoot = revDoc.RootElement;
                if (revRoot.TryGetProperty("surahOpenings", out var openings))
                {
                    foreach (var op in openings.EnumerateArray())
                    {
                        var page = op.GetProperty("pageNumber").GetInt32();
                        if (op.TryGetProperty("headerClipThresholdPercent", out var clipProp))
                        {
                            openingReviewClips[page] = clipProp.GetDouble();
                        }
                    }
                }
                else if (revRoot.TryGetProperty("Entries", out var entries) || revRoot.TryGetProperty("entries", out entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (entry.TryGetProperty("Page", out var pProp2) || entry.TryGetProperty("page", out pProp2))
                        {
                            var page = pProp2.GetInt32();
                            var lineCount = entry.TryGetProperty("LineCount", out var lcProp) ? lcProp.GetInt32() : 15;
                            var firstLine = entry.TryGetProperty("FirstTextLine", out var flProp) ? flProp.GetInt32() : 0;
                            if (lineCount > 0 && firstLine > 0)
                            {
                                openingReviewClips[page] = firstLine * 100.0 / lineCount;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Impossible de lire les clips d'ouverture : {ex.Message}");
            }
        }

        // Load QPC candidate texts if available
        var qpcTexts = new Dictionary<string, string>(); // "sura:ayah" -> text
        if (!string.IsNullOrWhiteSpace(qpcSourceJsonPath) && File.Exists(qpcSourceJsonPath))
        {
            try
            {
                using var qpcDoc = JsonDocument.Parse(await File.ReadAllTextAsync(qpcSourceJsonPath, cancellationToken));
                foreach (var item in qpcDoc.RootElement.EnumerateArray())
                {
                    var s = item.GetProperty("sura_no").GetInt32();
                    var a = item.GetProperty("aya_no").GetInt32();
                    var text = item.GetProperty("aya_text").GetString() ?? "";
                    qpcTexts[$"{s}:{a}"] = text;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Impossible de lire le texte QPC : {ex.Message}");
            }
        }

        // Manifest SHA-256
        using var sha256 = SHA256.Create();
        var manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
        var manifestSha = Convert.ToHexString(sha256.ComputeHash(manifestBytes)).ToLowerInvariant();

        var pagesTargetDir = Path.Combine(_workspaceService.WorkspacePath, "media", "mushaf", "pages");
        Directory.CreateDirectory(pagesTargetDir);

        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Insert Edition
            db.Editions.Add(new MushafEditionEntity
            {
                Code = editionCode,
                Revision = revision,
                ProvenanceUrl = provenance,
                ManifestSha256 = manifestSha,
                CountingSystem = "Madani / Warsh",
                ImportedAtUtc = DateTime.UtcNow
            });

            // Insert Surahs from Catalog
            var surahCatalog = SurahCatalogService.GetAllSurahs();
            foreach (var s in surahCatalog)
            {
                db.Surahs.Add(new SurahEntity
                {
                    Number = s.Number,
                    NameArabic = s.NameArabic,
                    NameLatin = s.NameLatin,
                    AyahCount = s.AyahCount,
                    StartPage = s.StartPage,
                    EndPage = s.EndPage
                });
            }

            var processedAyahs = new HashSet<string>();

            // Process Pages 1 to totalPages
            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ImportProgressReport(pageNum, totalPages, $"استيراد الصفحة {pageNum} من {totalPages}..."));

                var pageJsonName = $"page-{pageNum:D3}.json";
                var pagePngName = $"page-{pageNum:D3}.png";

                var sourceJson = Path.Combine(editionSourceDir, pageJsonName);
                var sourcePng = Path.Combine(editionSourceDir, pagePngName);

                if (!File.Exists(sourceJson) || !File.Exists(sourcePng))
                {
                    throw new FileNotFoundException($"Fichier manquant pour la page {pageNum}: {pageJsonName} ou {pagePngName}");
                }

                // Copy PNG to workspace target
                var targetPng = Path.Combine(pagesTargetDir, pagePngName);
                if (!File.Exists(targetPng))
                {
                    File.Copy(sourcePng, targetPng, overwrite: true);
                }

                // Compute PNG hash & verify expected hash
                var pngBytes = await File.ReadAllBytesAsync(targetPng, cancellationToken);
                var pngSha = Convert.ToHexString(sha256.ComputeHash(pngBytes)).ToLowerInvariant();
                if (expectedFileHashes.TryGetValue(pagePngName, out var expPngHash) &&
                    !string.Equals(pngSha, expPngHash, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.Instance.LogWarning($"Page {pageNum}: Empreinte PNG non conforme (obtenu {pngSha}, attendu {expPngHash})");
                }

                // Verify JSON hash if expected
                var jsonBytes = await File.ReadAllBytesAsync(sourceJson, cancellationToken);
                var jsonSha = Convert.ToHexString(sha256.ComputeHash(jsonBytes)).ToLowerInvariant();
                if (expectedFileHashes.TryGetValue(pageJsonName, out var expJsonHash) &&
                    !string.Equals(jsonSha, expJsonHash, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.Instance.LogWarning($"Page {pageNum}: Empreinte JSON non conforme (obtenu {jsonSha}, attendu {expJsonHash})");
                }

                int imgWidth = 1241;
                int imgHeight = 1754;
                try
                {
                    using var imgStream = File.OpenRead(targetPng);
                    using var bmp = new System.Drawing.Bitmap(imgStream);
                    imgWidth = bmp.Width;
                    imgHeight = bmp.Height;
                }
                catch
                {
                    // Fallback to standard Mushaf dimensions
                }

                db.Pages.Add(new MushafPageEntity
                {
                    PageNumber = pageNum,
                    EditionCode = editionCode,
                    RelativeImagePath = _workspaceService.ToRelativePath(targetPng),
                    Width = imgWidth,
                    Height = imgHeight,
                    Sha256 = pngSha
                });

                // Parse page JSON
                using var pageDoc = JsonDocument.Parse(jsonBytes);
                var pageRoot = pageDoc.RootElement;
                JsonElement cropBoxObj;
                JsonElement textBoxObj;

                if (pageRoot.TryGetProperty("boxes", out var boxesObj))
                {
                    cropBoxObj = boxesObj.GetProperty("cropBox");
                    textBoxObj = boxesObj.GetProperty("textBox");
                }
                else
                {
                    cropBoxObj = pageRoot.GetProperty("cropBox");
                    textBoxObj = pageRoot.GetProperty("textBox");
                }

                var cropBox = new NormalizedBox(
                    cropBoxObj.GetProperty("left").GetDouble(),
                    cropBoxObj.GetProperty("top").GetDouble(),
                    cropBoxObj.GetProperty("width").GetDouble(),
                    cropBoxObj.GetProperty("height").GetDouble());

                var textBox = new NormalizedBox(
                    textBoxObj.GetProperty("left").GetDouble(),
                    textBoxObj.GetProperty("top").GetDouble(),
                    textBoxObj.GetProperty("width").GetDouble(),
                    textBoxObj.GetProperty("height").GetDouble());

                double clipThreshold = openingReviewClips.TryGetValue(pageNum, out var ct) ? ct : 0.0;

                foreach (var row in pageRoot.GetProperty("rows").EnumerateArray())
                {
                    var surahNum = row.GetProperty("surah").GetInt32();
                    var ayahNum = row.GetProperty("ayah").GetInt32();
                    var verseNo = row.GetProperty("verseNo").GetInt32();
                    var ayahId = $"{surahNum}:{ayahNum}";

                    if (!processedAyahs.Contains(ayahId))
                    {
                        processedAyahs.Add(ayahId);
                        db.Ayahs.Add(new AyahEntity
                        {
                            Id = ayahId,
                            SurahNumber = surahNum,
                            AyahNumber = ayahNum,
                            VerseNo = verseNo,
                            PageNumber = pageNum
                        });

                        // Associate QPC text if available
                        if (qpcTexts.TryGetValue(ayahId, out var qpcText))
                        {
                            db.Texts.Add(new AyahTextEntity
                            {
                                CorpusId = "warsh-qpc-v10",
                                AyahId = ayahId,
                                SurahNumber = surahNum,
                                AyahNumber = ayahNum,
                                OriginalText = qpcText
                            });
                        }
                    }

                    int regionIndex = 0;
                    foreach (var rectItem in row.GetProperty("rects").EnumerateArray())
                    {
                        var rLeft = rectItem.GetProperty("left").GetDouble();
                        var rTop = rectItem.GetProperty("top").GetDouble();
                        var rWidth = rectItem.GetProperty("width").GetDouble();
                        var rHeight = rectItem.GetProperty("height").GetDouble();

                        var rawRect = new NormalizedBox(rLeft, rTop, rWidth, rHeight);
                        var pixelRect = AyahGeometry.ConvertToPixels(cropBox, textBox, rawRect, 1241, 1754, clipThreshold);

                        db.Regions.Add(new AyahRegionEntity
                        {
                            AyahId = ayahId,
                            PageNumber = pageNum,
                            OrderIndex = regionIndex++,
                            RawRectLeft = rLeft,
                            RawRectTop = rTop,
                            RawRectWidth = rWidth,
                            RawRectHeight = rHeight,
                            PixelX = pixelRect.X,
                            PixelY = pixelRect.Y,
                            PixelWidth = pixelRect.Width,
                            PixelHeight = pixelRect.Height
                        });
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LoggingService.Instance.LogInfo($"Import du pack terminé avec succès : {processedAyahs.Count} ayat importées.");
            return (true, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            LoggingService.Instance.LogError("Erreur lors de l'import du pack Mushaf", ex);
            return (false, $"فشل استيراد حزمة المصحف : {ex.Message}");
        }
    }
}
