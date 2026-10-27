using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class ScreenTools
{
    public static Color ParseColor(string value)
    {
        value = value.Trim();
        if (value.StartsWith('#') && value.Length == 7)
            return ColorTranslator.FromHtml(value);
        var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (hex) value = value[2..];
        if (!int.TryParse(value, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var code) || code < 0 || code > 0xFFFFFF)
            throw new ArgumentException("Цвет задаётся как #RRGGBB или десятичный COLORREF (0–16777215).");
        return Color.FromArgb(code & 255, (code >> 8) & 255, (code >> 16) & 255);
    }

    public static bool Matches(Color actual, Color expected, int tolerance) =>
        Math.Abs(actual.R - expected.R) <= tolerance &&
        Math.Abs(actual.G - expected.G) <= tolerance &&
        Math.Abs(actual.B - expected.B) <= tolerance;

    public static Bitmap Capture(Rectangle area)
    {
        var screen = SystemInformation.VirtualScreen;
        if (area.Width <= 0 || area.Height <= 0 || !screen.Contains(area))
            throw new ArgumentException($"Область {area} должна целиком находиться на рабочем столе {screen}.");
        if ((long)area.Width * area.Height > 30_000_000)
            throw new ArgumentException("Область снимка слишком велика (максимум 30 млн пикселей).");
        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public static Color Pixel(Point point)
    {
        using var image = Capture(new Rectangle(point, new Size(1, 1)));
        return image.GetPixel(0, 0);
    }

    public static Point? FindColor(Rectangle area, Color target, int tolerance, CancellationToken cancel)
    {
        using var image = Capture(area);
        var bytes = Bytes(image, out var stride);
        for (var y = 0; y < image.Height; y++)
        {
            cancel.ThrowIfCancellationRequested();
            for (var x = 0; x < image.Width; x++)
            {
                var p = y * stride + x * 4;
                if (Math.Abs(bytes[p] - target.B) <= tolerance &&
                    Math.Abs(bytes[p + 1] - target.G) <= tolerance &&
                    Math.Abs(bytes[p + 2] - target.R) <= tolerance)
                    return new Point(area.X + x, area.Y + y);
            }
        }
        return null;
    }

    public static Point? FindImage(Rectangle area, string file, int tolerance, CancellationToken cancel)
    {
        using var screenshot = Capture(area);
        using var source = new Bitmap(file);
        if (source.Width > screenshot.Width || source.Height > screenshot.Height)
            return null;
        if ((long)source.Width * source.Height > 2_000_000)
            throw new ArgumentException("Шаблон слишком велик (максимум 2 млн пикселей).");
        using var template = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(template)) g.DrawImageUnscaled(source, 0, 0);
        var screenBytes = Bytes(screenshot, out var screenStride);
        var templateBytes = Bytes(template, out var templateStride);
        var first = FirstOpaque(templateBytes, templateStride, template.Width, template.Height);
        if (first is null) throw new ArgumentException("Шаблон полностью прозрачный.");
        var (firstX, firstY) = first.Value;
        for (var y = 0; y <= screenshot.Height - template.Height; y++)
        {
            cancel.ThrowIfCancellationRequested();
            for (var x = 0; x <= screenshot.Width - template.Width; x++)
            {
                if (!Same(screenBytes, (y + firstY) * screenStride + (x + firstX) * 4,
                    templateBytes, firstY * templateStride + firstX * 4, tolerance)) continue;
                var match = true;
                for (var ty = 0; ty < template.Height && match; ty++)
                    for (var tx = 0; tx < template.Width; tx++)
                    {
                        var tp = ty * templateStride + tx * 4;
                        if (templateBytes[tp + 3] == 0) continue;
                        if (!Same(screenBytes, (y + ty) * screenStride + (x + tx) * 4, templateBytes, tp, tolerance))
                        { match = false; break; }
                    }
                if (match) return new Point(area.X + x, area.Y + y);
            }
        }
        return null;
    }

    public static void SavePng(Rectangle area, string path)
    {
        using var image = Capture(area);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory != null) Directory.CreateDirectory(directory);
        image.Save(path, ImageFormat.Png);
    }

    private static byte[] Bytes(Bitmap bitmap, out int stride)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            stride = data.Stride;
            var bytes = new byte[stride * bitmap.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static (int X, int Y)? FirstOpaque(byte[] bytes, int stride, int width, int height)
    {
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (bytes[y * stride + x * 4 + 3] != 0) return (x, y);
        return null;
    }

    private static bool Same(byte[] screen, int s, byte[] template, int t, int tolerance) =>
        Math.Abs(screen[s] - template[t]) <= tolerance &&
        Math.Abs(screen[s + 1] - template[t + 1]) <= tolerance &&
        Math.Abs(screen[s + 2] - template[t + 2]) <= tolerance;
}
