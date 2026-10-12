using QuranVideo.Desktop.Models;
using Xunit;

namespace QuranVideo.Tests;

public class GeometryTests
{
    [Fact]
    public void ConvertToPixels_WithIdentityBoxes_MapsToExactDimensions()
    {
        // 100% crop, 100% text, 50% left/top 50% w/h on 1000x1000 image
        var crop = new NormalizedBox(0, 0, 100, 100);
        var text = new NormalizedBox(0, 0, 100, 100);
        var rect = new NormalizedBox(50, 50, 50, 50);

        var pixelRect = AyahGeometry.ConvertToPixels(crop, text, rect, 1000, 1000);

        Assert.Equal(500.0, pixelRect.X, precision: 4);
        Assert.Equal(500.0, pixelRect.Y, precision: 4);
        Assert.Equal(500.0, pixelRect.Width, precision: 4);
        Assert.Equal(500.0, pixelRect.Height, precision: 4);
    }

    [Fact]
    public void ConvertToPixels_WithNestedCropAndTextBox_ComputesExactFormula()
    {
        // Image 1241 x 1754
        // Crop: left 10%, top 10%, width 80%, height 80%
        // Text: left 5%, top 5%, width 90%, height 90%
        // Rect: left 10%, top 20%, width 40%, height 10%
        var crop = new NormalizedBox(10, 10, 80, 80);
        var text = new NormalizedBox(5, 5, 90, 90);
        var rect = new NormalizedBox(10, 20, 40, 10);

        var pixelRect = AyahGeometry.ConvertToPixels(crop, text, rect, 1241, 1754);

        // Expected X: 1241 * (0.10 + 0.80 * (0.05 + 0.90 * 0.10)) = 1241 * (0.10 + 0.80 * 0.14) = 1241 * 0.212 = 263.092
        // Expected W: 1241 * (0.80 * 0.90 * 0.40) = 1241 * 0.288 = 357.408
        Assert.Equal(263.092, pixelRect.X, precision: 3);
        Assert.Equal(357.408, pixelRect.Width, precision: 3);
        Assert.True(pixelRect.X >= 0 && pixelRect.Right <= 1241);
        Assert.True(pixelRect.Y >= 0 && pixelRect.Bottom <= 1754);
    }

    [Fact]
    public void ConvertToPixels_WithMinimumTopClip_ClipsTopAndReducesHeight()
    {
        var crop = new NormalizedBox(0, 0, 100, 100);
        var text = new NormalizedBox(0, 0, 100, 100);
        var rect = new NormalizedBox(0, 10, 100, 30); // Top: 10%, Height: 30% -> Bottom is 40%

        // If minimumTop is 25%:
        // RectTop becomes 25%, RectHeight becomes 40 - 25 = 15%
        var pixelRect = AyahGeometry.ConvertToPixels(crop, text, rect, 1000, 1000, minimumTopPercent: 25.0);

        Assert.Equal(250.0, pixelRect.Y, precision: 4);
        Assert.Equal(150.0, pixelRect.Height, precision: 4);
    }

    [Fact]
    public void ConvertToPixels_WhenMinimumTopExceedsBottom_ReturnsEmptyRect()
    {
        var crop = new NormalizedBox(0, 0, 100, 100);
        var text = new NormalizedBox(0, 0, 100, 100);
        var rect = new NormalizedBox(0, 5, 100, 10); // Bottom is 15%

        var pixelRect = AyahGeometry.ConvertToPixels(crop, text, rect, 1000, 1000, minimumTopPercent: 20.0);

        Assert.Equal(0.0, pixelRect.Width);
        Assert.Equal(0.0, pixelRect.Height);
    }

    [Fact]
    public void ComputeEnclosingBoundingBox_IncludesAllRectanglesWithPadding()
    {
        var rects = new[]
        {
            new PixelRect(100, 200, 50, 20),
            new PixelRect(80, 250, 120, 25),
            new PixelRect(150, 300, 40, 20)
        };

        var bbox = AyahGeometry.ComputeEnclosingBoundingBox(rects, padding: 10);

        Assert.Equal(70.0, bbox.X);      // 80 - 10
        Assert.Equal(190.0, bbox.Y);     // 200 - 10
        Assert.Equal(140.0, bbox.Width); // (80 + 120 + 10) - 70 = 210 - 70 = 140
        Assert.Equal(140.0, bbox.Height);// (300 + 20 + 10) - 190 = 330 - 190 = 140
    }
}
