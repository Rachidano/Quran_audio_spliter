namespace QuranVideo.Desktop.Models;

public readonly record struct NormalizedBox(double Left, double Top, double Width, double Height);

public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public int RoundX => (int)Math.Floor(X);
    public int RoundY => (int)Math.Floor(Y);
    public int RoundWidth => (int)Math.Ceiling(X + Width) - RoundX;
    public int RoundHeight => (int)Math.Ceiling(Y + Height) - RoundY;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public static class AyahGeometry
{
    /// <summary>
    /// Converts nested percentage boxes (cropBox -> textBox -> rect) into exact pixel coordinates.
    /// Formula from CONCEPTION_DETAILLEE.md:
    /// x = W * [cL/100 + cW/100 * (tL/100 + tW/100 * rL/100)]
    /// w = W * cW/100 * tW/100 * rW/100
    /// </summary>
    public static PixelRect ConvertToPixels(
        NormalizedBox cropBox,
        NormalizedBox textBox,
        NormalizedBox rect,
        int imageWidth,
        int imageHeight,
        double minimumTopPercent = 0.0)
    {
        var rectTop = rect.Top;
        var rectHeight = rect.Height;

        // Apply opening header clip if minimumTopPercent is specified
        if (minimumTopPercent > 0.0)
        {
            var bottom = rectTop + rectHeight;
            rectTop = Math.Max(rectTop, minimumTopPercent);
            rectHeight = bottom - rectTop;
            if (rectHeight <= 1e-8)
            {
                return new PixelRect(0, 0, 0, 0);
            }
        }

        var x = imageWidth * (cropBox.Left / 100.0 + (cropBox.Width / 100.0) * (textBox.Left / 100.0 + (textBox.Width / 100.0) * (rect.Left / 100.0)));
        var y = imageHeight * (cropBox.Top / 100.0 + (cropBox.Height / 100.0) * (textBox.Top / 100.0 + (textBox.Height / 100.0) * (rectTop / 100.0)));
        var w = imageWidth * (cropBox.Width / 100.0) * (textBox.Width / 100.0) * (rect.Width / 100.0);
        var h = imageHeight * (cropBox.Height / 100.0) * (textBox.Height / 100.0) * (rectHeight / 100.0);

        return new PixelRect(x, y, w, h);
    }

    /// <summary>
    /// Computes the collective bounding box enclosing all valid rectangles of an ayah.
    /// </summary>
    public static PixelRect ComputeEnclosingBoundingBox(IReadOnlyList<PixelRect> rects, int padding = 6)
    {
        var valid = rects.Where(r => r.Width > 0 && r.Height > 0).ToList();
        if (valid.Count == 0) return new PixelRect(0, 0, 0, 0);

        var minX = valid.Min(r => r.X) - padding;
        var minY = valid.Min(r => r.Y) - padding;
        var maxX = valid.Max(r => r.Right) + padding;
        var maxY = valid.Max(r => r.Bottom) + padding;

        return new PixelRect(Math.Max(0, minX), Math.Max(0, minY), Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }
}
