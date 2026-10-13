using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class AyahCardComposerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testAyahImagePath;
    private readonly string _testLogoPath;
    private readonly string _testBgPath;

    public AyahCardComposerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"card_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // 1. Create a dummy original ayah calligraphy image (white background with dark pixels)
        _testAyahImagePath = Path.Combine(_tempDir, "ayah_001.png");
        using (var bmp = new Bitmap(600, 120))
        {
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var brush = new SolidBrush(Color.Black);
            g.FillEllipse(brush, 50, 40, 40, 40); // mock Arabic letter/sign
            bmp.Save(_testAyahImagePath, ImageFormat.Png);
        }

        // 2. Create a dummy watermark logo PNG
        _testLogoPath = Path.Combine(_tempDir, "logo.png");
        using (var bmp = new Bitmap(150, 60))
        {
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(200, 30, 41, 59));
            g.FillRectangle(brush, 0, 0, 150, 60);
            bmp.Save(_testLogoPath, ImageFormat.Png);
        }

        // 3. Create a dummy custom background image
        _testBgPath = Path.Combine(_tempDir, "bg.jpg");
        using (var bmp = new Bitmap(800, 600))
        {
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.DarkOliveGreen);
            bmp.Save(_testBgPath, ImageFormat.Jpeg);
        }
    }

    [Fact]
    public void ComposeCard_LightBackground_GeneratesPureWhiteFrame()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Light);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // Top-left pixel should be pure white
        var cornerPixel = bitmap.GetPixel(10, 10);
        Assert.Equal(255, cornerPixel.R);
        Assert.Equal(255, cornerPixel.G);
        Assert.Equal(255, cornerPixel.B);
    }

    [Fact]
    public void ComposeCard_DarkBackground_DrawsDarkCanvasWithWhiteCardInCenter()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Dark);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // Outer corner should be dark (slate-900: #0F172A = R:15, G:23, B:42)
        var outerPixel = bitmap.GetPixel(20, 20);
        Assert.True(outerPixel.R < 30 && outerPixel.G < 35 && outerPixel.B < 55, "Outer pixel should be dark slate");

        // Center pixel inside the sacred white card should be pure white (#FFFFFF)
        var centerPixel = bitmap.GetPixel(1920 / 2, 1080 / 2);
        Assert.Equal(255, centerPixel.R);
        Assert.Equal(255, centerPixel.G);
        Assert.Equal(255, centerPixel.B);
    }

    [Fact]
    public void ComposeCard_CustomImageBackground_DrawsCustomImageWithCenteredCard()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.CustomImage,
            CustomBackgroundPath: _testBgPath);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // Center pixel inside the sacred card should be pure white
        var centerPixel = bitmap.GetPixel(1920 / 2, 1080 / 2);
        Assert.Equal(255, centerPixel.R);
        Assert.Equal(255, centerPixel.G);
        Assert.Equal(255, centerPixel.B);
    }

    [Fact]
    public void ComposeCard_VerticalOrientation_Produces1080x1920Canvas()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1080,
            TargetHeight: 1920,
            BackgroundType: BackgroundType.Dark);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1080, bitmap.Width);
        Assert.Equal(1920, bitmap.Height);

        // Top margin should be dark
        var topPixel = bitmap.GetPixel(540, 50);
        Assert.True(topPixel.R < 30 && topPixel.G < 35, "Top margin should be dark background in vertical orientation");
    }

    [Theory]
    [InlineData(WatermarkPosition.TopLeft)]
    [InlineData(WatermarkPosition.TopRight)]
    [InlineData(WatermarkPosition.BottomLeft)]
    [InlineData(WatermarkPosition.BottomRight)]
    public void ComposeCard_WithArabicTextWatermark_RendersInAllFourCorners(WatermarkPosition pos)
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Dark,
            WatermarkType: WatermarkType.Text,
            WatermarkText: "قناة القرآن الكريم",
            WatermarkPosition: pos,
            WatermarkOpacity: 0.9);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // Center remains intact and white
        var centerPixel = bitmap.GetPixel(1920 / 2, 1080 / 2);
        Assert.Equal(255, centerPixel.R);
        Assert.Equal(255, centerPixel.G);
        Assert.Equal(255, centerPixel.B);
    }

    [Fact]
    public void ComposeCard_WithLogoWatermark_AppliesOpacityCorrectly()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Light,
            WatermarkType: WatermarkType.Logo,
            WatermarkImagePath: _testLogoPath,
            WatermarkPosition: WatermarkPosition.BottomRight,
            WatermarkOpacity: 0.5);

        using var bitmap = AyahCardComposer.ComposeCardBitmap(_testAyahImagePath, settings);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);
    }

    [Fact]
    public void ComposeCard_BitmapSource_MatchesDimensionsForWpfPreview()
    {
        var settings = new CardCompositionSettings(
            TargetWidth: 1920,
            TargetHeight: 1080,
            BackgroundType: BackgroundType.Dark);

        var bitmapSource = AyahCardComposer.ComposeCardToBitmapSource(_testAyahImagePath, settings);

        Assert.NotNull(bitmapSource);
        Assert.Equal(1920, bitmapSource.PixelWidth);
        Assert.Equal(1080, bitmapSource.PixelHeight);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }
}
