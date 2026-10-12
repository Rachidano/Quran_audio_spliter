using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

Console.WriteLine("Generating high quality QuranVideo app.ico...");

var desktopDir = Directory.Exists("desktop") ? Path.GetFullPath("desktop") : @"D:\Projets\ITComp\Quran\desktop";
var targetDir = Path.Combine(desktopDir, "src", "QuranVideo.Desktop", "Resources");
Directory.CreateDirectory(targetDir);
var targetPath = Path.Combine(targetDir, "app.ico");
var packagingPath = Path.Combine(desktopDir, "packaging", "app.ico");

int[] sizes = [256, 128, 64, 48, 32, 16];
var images = new List<(int Size, byte[] PngData)>();

foreach (var size in sizes)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float margin = Math.Max(1f, size * 0.04f);
        var rect = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
        float radius = size * 0.22f;

        // Rounded squircle background
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();

        // Emerald gradient
        using var bgBrush = new LinearGradientBrush(
            rect,
            Color.FromArgb(255, 16, 130, 68),
            Color.FromArgb(255, 6, 78, 38),
            45f);
        g.FillPath(bgBrush, path);

        // Gold border
        float penWidth = Math.Max(1f, size * 0.025f);
        using var goldPen = new Pen(Color.FromArgb(235, 245, 195, 65), penWidth);
        g.DrawPath(goldPen, path);

        float scale = size / 256f;

        // Holy Quran Open Book Pages
        using var bookBrush = new SolidBrush(Color.FromArgb(252, 255, 255, 255));
        using var goldBookPen = new Pen(Color.FromArgb(255, 225, 175, 45), Math.Max(1f, 3.5f * scale));

        // Left Page
        using (var leftPage = new GraphicsPath())
        {
            leftPage.AddBezier(128f * scale, 188f * scale, 95f * scale, 198f * scale, 60f * scale, 192f * scale, 42f * scale, 178f * scale);
            leftPage.AddLine(42f * scale, 178f * scale, 42f * scale, 98f * scale);
            leftPage.AddBezier(42f * scale, 98f * scale, 65f * scale, 114f * scale, 100f * scale, 115f * scale, 128f * scale, 108f * scale);
            leftPage.CloseFigure();
            g.FillPath(bookBrush, leftPage);
            g.DrawPath(goldBookPen, leftPage);
        }

        // Right Page
        using (var rightPage = new GraphicsPath())
        {
            rightPage.AddBezier(128f * scale, 188f * scale, 161f * scale, 198f * scale, 196f * scale, 192f * scale, 214f * scale, 178f * scale);
            rightPage.AddLine(214f * scale, 178f * scale, 214f * scale, 98f * scale);
            rightPage.AddBezier(214f * scale, 98f * scale, 191f * scale, 114f * scale, 156f * scale, 115f * scale, 128f * scale, 108f * scale);
            rightPage.CloseFigure();
            g.FillPath(bookBrush, rightPage);
            g.DrawPath(goldBookPen, rightPage);
        }

        // Spine Center Line
        using (var spinePen = new Pen(Color.FromArgb(255, 185, 135, 35), Math.Max(1f, 4.5f * scale)))
        {
            g.DrawLine(spinePen, 128f * scale, 106f * scale, 128f * scale, 190f * scale);
        }

        // Quran Lines Calligraphy marks (at larger scales)
        if (size >= 48)
        {
            using var linePen = new Pen(Color.FromArgb(120, 16, 120, 60), Math.Max(1f, 2f * scale));
            // Left page lines
            g.DrawLine(linePen, 58f * scale, 125f * scale, 116f * scale, 122f * scale);
            g.DrawLine(linePen, 58f * scale, 142f * scale, 116f * scale, 139f * scale);
            g.DrawLine(linePen, 58f * scale, 159f * scale, 116f * scale, 156f * scale);

            // Right page lines
            g.DrawLine(linePen, 140f * scale, 122f * scale, 198f * scale, 125f * scale);
            g.DrawLine(linePen, 140f * scale, 139f * scale, 198f * scale, 142f * scale);
            g.DrawLine(linePen, 140f * scale, 156f * scale, 198f * scale, 159f * scale);
        }

        // Video Play Badge on top center
        var playRect = new RectangleF(108f * scale, 50f * scale, 40f * scale, 40f * scale);
        using var playBgBrush = new LinearGradientBrush(playRect, Color.FromArgb(255, 248, 202, 68), Color.FromArgb(255, 218, 148, 24), 45f);
        g.FillEllipse(playBgBrush, playRect);

        using var playTriBrush = new SolidBrush(Color.FromArgb(255, 16, 95, 48));
        PointF[] tri = [
            new(124f * scale, 63f * scale),
            new(124f * scale, 77f * scale),
            new(136f * scale, 70f * scale)
        ];
        g.FillPolygon(playTriBrush, tri);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    images.Add((size, ms.ToArray()));
}

// Write standard multi-resolution ICO file
using (var fs = File.Create(targetPath))
using (var bw = new BinaryWriter(fs))
{
    // ICONDIR
    bw.Write((ushort)0); // Reserved
    bw.Write((ushort)1); // Type 1 (Icon)
    bw.Write((ushort)images.Count);

    int offset = 6 + (images.Count * 16);

    foreach (var (size, pngBytes) in images)
    {
        bw.Write((byte)(size >= 256 ? 0 : size)); // Width
        bw.Write((byte)(size >= 256 ? 0 : size)); // Height
        bw.Write((byte)0); // Colors
        bw.Write((byte)0); // Reserved
        bw.Write((ushort)1); // Planes
        bw.Write((ushort)32); // BPP
        bw.Write((uint)pngBytes.Length);
        bw.Write((uint)offset);
        offset += pngBytes.Length;
    }

    foreach (var (_, pngBytes) in images)
    {
        bw.Write(pngBytes);
    }
}

File.Copy(targetPath, packagingPath, true);
Console.WriteLine($"Icon created: {targetPath} and {packagingPath} ({new FileInfo(targetPath).Length} bytes)");
