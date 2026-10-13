using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public sealed record CardCompositionSettings(
    int TargetWidth,
    int TargetHeight,
    BackgroundType BackgroundType = BackgroundType.Light,
    string? CustomBackgroundPath = null,
    WatermarkType WatermarkType = WatermarkType.None,
    string? WatermarkImagePath = null,
    string? WatermarkText = null,
    WatermarkPosition WatermarkPosition = WatermarkPosition.BottomRight,
    double WatermarkOpacity = 0.8,
    double WatermarkScale = 1.0)
{
    public static CardCompositionSettings CreateDefault(VideoOrientation orientation)
    {
        var (w, h) = orientation == VideoOrientation.Horizontal
            ? (1920, 1080)
            : (1080, 1920);

        return new CardCompositionSettings(
            TargetWidth: w,
            TargetHeight: h,
            BackgroundType: BackgroundType.Light,
            WatermarkType: WatermarkType.None,
            WatermarkPosition: WatermarkPosition.BottomRight,
            WatermarkOpacity: 0.8,
            WatermarkScale: 1.0);
    }
}

public static class AyahCardComposer
{
    private static readonly Color DarkBackgroundColor = Color.FromArgb(15, 23, 42); // slate-900
    private static readonly Color CardBorderColor = Color.FromArgb(203, 213, 225); // slate-300

    public static Bitmap ComposeCardBitmap(string ayahImagePath, CardCompositionSettings settings)
    {
        var targetW = settings.TargetWidth > 0 ? settings.TargetWidth : 1920;
        var targetH = settings.TargetHeight > 0 ? settings.TargetHeight : 1080;

        var canvas = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(canvas);

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Draw Background
        DrawBackground(g, targetW, targetH, settings);

        // 2. Calculate Card & Ayah Geometry
        Rectangle cardRect;
        if (File.Exists(ayahImagePath))
        {
            using var ayahImg = Image.FromFile(ayahImagePath);
            cardRect = DrawAyahCard(g, targetW, targetH, ayahImg, settings);
        }
        else
        {
            cardRect = new Rectangle(targetW / 4, targetH / 4, targetW / 2, targetH / 2);
        }

        // 3. Draw Watermark if configured, ensuring no overlap with cardRect
        if (settings.WatermarkType != WatermarkType.None)
        {
            DrawWatermark(g, targetW, targetH, cardRect, settings);
        }

        return canvas;
    }

    public static void ComposeCardToFile(string ayahImagePath, CardCompositionSettings settings, string outputPngPath)
    {
        var dir = Path.GetDirectoryName(outputPngPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var bmp = ComposeCardBitmap(ayahImagePath, settings);
        bmp.Save(outputPngPath, ImageFormat.Png);
    }

    public static BitmapSource ComposeCardToBitmapSource(string ayahImagePath, CardCompositionSettings settings)
    {
        using var bmp = ComposeCardBitmap(ayahImagePath, settings);
        using var memory = new MemoryStream();
        bmp.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = memory;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    private static void DrawBackground(Graphics g, int targetW, int targetH, CardCompositionSettings settings)
    {
        switch (settings.BackgroundType)
        {
            case BackgroundType.Dark:
                using (var darkBrush = new SolidBrush(DarkBackgroundColor))
                {
                    g.FillRectangle(darkBrush, 0, 0, targetW, targetH);
                }
                break;

            case BackgroundType.CustomImage when !string.IsNullOrWhiteSpace(settings.CustomBackgroundPath) && File.Exists(settings.CustomBackgroundPath):
                try
                {
                    using var bgImg = Image.FromFile(settings.CustomBackgroundPath);
                    DrawAspectFill(g, bgImg, targetW, targetH);
                }
                catch
                {
                    using var fallbackBrush = new SolidBrush(DarkBackgroundColor);
                    g.FillRectangle(fallbackBrush, 0, 0, targetW, targetH);
                }
                break;

            case BackgroundType.Light:
            default:
                g.Clear(Color.White);
                break;
        }
    }

    private static void DrawAspectFill(Graphics g, Image img, int targetW, int targetH)
    {
        var scale = Math.Max((double)targetW / img.Width, (double)targetH / img.Height);
        var scaledW = (int)Math.Ceiling(img.Width * scale);
        var scaledH = (int)Math.Ceiling(img.Height * scale);
        var srcX = (targetW - scaledW) / 2;
        var srcY = (targetH - scaledH) / 2;

        g.DrawImage(img, srcX, srcY, scaledW, scaledH);
    }

    private static Rectangle DrawAyahCard(Graphics g, int targetW, int targetH, Image ayahImg, CardCompositionSettings settings)
    {
        bool isHorizontal = targetW >= targetH;
        int maxCardW = isHorizontal ? (int)(targetW * 0.82) : (int)(targetW * 0.90);
        int maxCardH = isHorizontal ? (int)(targetH * 0.80) : (int)(targetH * 0.75);

        const int padding = 24;
        int innerMaxW = Math.Max(100, maxCardW - (padding * 2));
        int innerMaxH = Math.Max(100, maxCardH - (padding * 2));

        double scale = Math.Min((double)innerMaxW / ayahImg.Width, (double)innerMaxH / ayahImg.Height);
        // Avoid excessive upscaling if image is already sufficiently large
        if (scale > 1.25) scale = 1.25;

        int scaledAyahW = (int)(ayahImg.Width * scale);
        int scaledAyahH = (int)(ayahImg.Height * scale);

        if (settings.BackgroundType == BackgroundType.Light)
        {
            // In pure light mode: ayah is centered directly
            int ayahX = (targetW - scaledAyahW) / 2;
            int ayahY = (targetH - scaledAyahH) / 2;
            g.DrawImage(ayahImg, ayahX, ayahY, scaledAyahW, scaledAyahH);

            return new Rectangle(ayahX, ayahY, scaledAyahW, scaledAyahH);
        }
        else
        {
            // Dark or CustomImage: draw sacred white card container
            int cardW = scaledAyahW + (padding * 2);
            int cardH = scaledAyahH + (padding * 2);
            int cardX = (targetW - cardW) / 2;
            int cardY = (targetH - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Draw white card with subtle shadow / rounded border
            using (var path = CreateRoundedRectanglePath(cardRect, 16))
            {
                using var whiteBrush = new SolidBrush(Color.White);
                g.FillPath(whiteBrush, path);

                using var borderPen = new Pen(CardBorderColor, 1.5f);
                g.DrawPath(borderPen, path);
            }

            // Draw original calligraphy inside card at 100% pixel fidelity
            int ayahX = cardX + padding;
            int ayahY = cardY + padding;
            g.DrawImage(ayahImg, ayahX, ayahY, scaledAyahW, scaledAyahH);

            return cardRect;
        }
    }

    private static void DrawWatermark(Graphics g, int targetW, int targetH, Rectangle cardRect, CardCompositionSettings settings)
    {
        float opacity = (float)Math.Clamp(settings.WatermarkOpacity, 0.05, 1.0);
        float scaleMultiplier = (float)Math.Clamp(settings.WatermarkScale > 0 ? settings.WatermarkScale : 1.0, 0.3, 3.0);
        const int margin = 48;

        if (settings.WatermarkType == WatermarkType.Logo && !string.IsNullOrWhiteSpace(settings.WatermarkImagePath) && File.Exists(settings.WatermarkImagePath))
        {
            try
            {
                using var wmImg = Image.FromFile(settings.WatermarkImagePath);
                int maxWmW = (int)(240 * scaleMultiplier);
                int maxWmH = (int)(90 * scaleMultiplier);

                double scale = Math.Min((double)maxWmW / wmImg.Width, (double)maxWmH / wmImg.Height);
                if (scale > scaleMultiplier) scale = scaleMultiplier;

                int wmW = Math.Max(1, (int)(wmImg.Width * scale));
                int wmH = Math.Max(1, (int)(wmImg.Height * scale));

                var wmRect = CalculateCornerPosition(settings.WatermarkPosition, targetW, targetH, wmW, wmH, margin);
                wmRect.X = Math.Clamp(wmRect.X, 0, Math.Max(0, targetW - wmW));
                wmRect.Y = Math.Clamp(wmRect.Y, 0, Math.Max(0, targetH - wmH));

                using var attributes = new ImageAttributes();
                var matrix = new ColorMatrix
                {
                    Matrix33 = opacity
                };
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.DrawImage(wmImg, wmRect, 0, 0, wmImg.Width, wmImg.Height, GraphicsUnit.Pixel, attributes);
            }
            catch
            {
                // Ignore watermark image load errors gracefully
            }
        }
        else if (settings.WatermarkType == WatermarkType.Text && !string.IsNullOrWhiteSpace(settings.WatermarkText))
        {
            float fontSize = (float)Math.Clamp(24f * scaleMultiplier, 10f, 96f);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var textSize = g.MeasureString(settings.WatermarkText, font);
            int textW = (int)Math.Ceiling(textSize.Width);
            int textH = (int)Math.Ceiling(textSize.Height);

            var textRect = CalculateCornerPosition(settings.WatermarkPosition, targetW, targetH, textW, textH, margin);
            textRect.X = Math.Clamp(textRect.X, 0, Math.Max(0, targetW - textW));
            textRect.Y = Math.Clamp(textRect.Y, 0, Math.Max(0, targetH - textH));

            int alpha = (int)(opacity * 255);
            var textColor = settings.BackgroundType == BackgroundType.Light
                ? Color.FromArgb(alpha, 51, 65, 85) // slate-700
                : Color.FromArgb(alpha, 248, 250, 252); // slate-50

            using var brush = new SolidBrush(textColor);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(settings.WatermarkText, font, brush, textRect, format);
        }
    }

    private static Rectangle CalculateCornerPosition(WatermarkPosition pos, int targetW, int targetH, int itemW, int itemH, int margin)
    {
        return pos switch
        {
            WatermarkPosition.TopLeft => new Rectangle(margin, margin, itemW, itemH),
            WatermarkPosition.TopRight => new Rectangle(targetW - margin - itemW, margin, itemW, itemH),
            WatermarkPosition.BottomLeft => new Rectangle(margin, targetH - margin - itemH, itemW, itemH),
            WatermarkPosition.BottomRight or _ => new Rectangle(targetW - margin - itemW, targetH - margin - itemH, itemW, itemH)
        };
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;

        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }
}
