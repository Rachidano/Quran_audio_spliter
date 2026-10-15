using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using QuranVideo.Desktop.Models;

namespace QuranVideo.Desktop.Services;

public static class AyahImageExtractor
{
    /// <summary>
    /// Generates the isolated Ayah image on a clean background (e.g. white),
    /// preserving relative line positions, whitespace indentations, ink pixels, and ayah medallion.
    /// </summary>
    public static Bitmap ExtractIsolatedAyah(
        string sourceImagePath,
        IReadOnlyList<PixelRect> rects,
        PixelRect boundingBox,
        Color backgroundColor)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(sourceImagePath));
        using var sourceImage = new Bitmap(stream);
        var destWidth = Math.Max(1, boundingBox.RoundWidth);
        var destHeight = Math.Max(1, boundingBox.RoundHeight);

        var destBitmap = new Bitmap(destWidth, destHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(destBitmap);
        g.Clear(backgroundColor);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        foreach (var r in rects)
        {
            if (r.Width <= 0 || r.Height <= 0) continue;

            // Rogner au cadre de la page en gardant la position relative (un rectangle élargi
            // par les marges peut déborder de la page)
            var srcX = Math.Max(0, r.RoundX);
            var srcY = Math.Max(0, r.RoundY);
            var srcW = Math.Min(sourceImage.Width, r.RoundX + r.RoundWidth) - srcX;
            var srcH = Math.Min(sourceImage.Height, r.RoundY + r.RoundHeight) - srcY;

            if (srcW <= 0 || srcH <= 0) continue;

            var dstX = srcX - boundingBox.RoundX;
            var dstY = srcY - boundingBox.RoundY;

            var srcRect = new Rectangle(srcX, srcY, srcW, srcH);
            var dstRect = new Rectangle(dstX, dstY, srcW, srcH);

            g.DrawImage(sourceImage, dstRect, srcRect, GraphicsUnit.Pixel);
        }

        return destBitmap;
    }

    /// <summary>
    /// Generates a mask overlay onto the original page for side-by-side comparison.
    /// Highlights the ayah regions in semi-transparent amber with a solid border.
    /// </summary>
    public static Bitmap GenerateMaskOverlay(
        string sourceImagePath,
        IReadOnlyList<PixelRect> rects)
    {
        using var overlayStream = new MemoryStream(File.ReadAllBytes(sourceImagePath));
        var overlayBitmap = new Bitmap(overlayStream);
        using var g = Graphics.FromImage(overlayBitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var fillBrush = new SolidBrush(Color.FromArgb(80, 227, 183, 59));
        using var borderPen = new Pen(Color.FromArgb(220, 170, 125, 18), 2.0f);

        foreach (var r in rects)
        {
            if (r.Width <= 0 || r.Height <= 0) continue;
            var rect = new Rectangle(r.RoundX, r.RoundY, r.RoundWidth, r.RoundHeight);
            g.FillRectangle(fillBrush, rect);
            g.DrawRectangle(borderPen, rect);
        }

        return overlayBitmap;
    }

    /// <summary>
    /// Produces a side-by-side comparison image:
    /// [Page Originale] | [Masque Superposé] | [Image Isolée sur Fond Blanc]
    /// </summary>
    public static Bitmap GenerateSideBySideComparison(
        string sourceImagePath,
        IReadOnlyList<PixelRect> rects,
        PixelRect boundingBox)
    {
        using var origStream = new MemoryStream(File.ReadAllBytes(sourceImagePath));
        using var original = new Bitmap(origStream);
        using var overlay = GenerateMaskOverlay(sourceImagePath, rects);
        using var isolated = ExtractIsolatedAyah(sourceImagePath, rects, boundingBox, Color.White);

        int pad = 20;
        int targetH = original.Height;
        int pageW = original.Width;

        double scale = Math.Min(1.5, (double)(targetH - 100) / isolated.Height);
        int isoDisplayW = Math.Max(pageW, (int)(isolated.Width * scale) + 40);
        int totalW = pageW * 2 + isoDisplayW + pad * 4;

        var compBitmap = new Bitmap(totalW, targetH + 80, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(compBitmap);
        g.Clear(Color.FromArgb(244, 243, 237));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(25, 61, 50));

        // Panel 1: Original
        g.DrawString("1. Page originale", titleFont, textBrush, pad, 15);
        g.DrawImage(original, pad, 50, pageW, targetH);

        // Panel 2: Mask Overlay
        int x2 = pad * 2 + pageW;
        g.DrawString("2. Masque superposé", titleFont, textBrush, x2, 15);
        g.DrawImage(overlay, x2, 50, pageW, targetH);

        // Panel 3: Isolated Ayah
        int x3 = pad * 3 + pageW * 2;
        g.DrawString("3. Image extraite (fond blanc)", titleFont, textBrush, x3, 15);

        int destIsoW = (int)(isolated.Width * scale);
        int destIsoH = (int)(isolated.Height * scale);
        int isoX = x3 + (isoDisplayW - destIsoW) / 2;
        int isoY = 50 + (targetH - destIsoH) / 2;

        using var cardBrush = new SolidBrush(Color.White);
        using var cardBorder = new Pen(Color.FromArgb(200, 200, 200), 1);
        g.FillRectangle(cardBrush, x3, 50, isoDisplayW, targetH);
        g.DrawRectangle(cardBorder, x3, 50, isoDisplayW, targetH);

        g.DrawImage(isolated, isoX, isoY, destIsoW, destIsoH);

        return compBitmap;
    }
}
