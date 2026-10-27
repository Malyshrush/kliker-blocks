using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

// A stable snapshot for repeated pixel analysis. All methods use screen coordinates.
internal sealed class ScreenAnalysisBuffer
{
    private readonly object _gate = new();
    private readonly int[] _colors;
    public Rectangle Bounds { get; }

    internal ScreenAnalysisBuffer(Rectangle bounds, int[] colors)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 ||
            (long)bounds.Width * bounds.Height != colors.Length)
            throw new ArgumentException("Размер графического буфера не совпадает с числом пикселей.");
        Bounds = bounds;
        _colors = (int[])colors.Clone();
    }

    public static ScreenAnalysisBuffer Capture(Rectangle bounds)
    {
        using var bitmap = ScreenTools.Capture(bounds);
        return FromBitmap(bounds, bitmap);
    }

    private static ScreenAnalysisBuffer FromBitmap(Rectangle bounds, Bitmap bitmap)
    {
        using var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(converted)) graphics.DrawImageUnscaled(bitmap, 0, 0);
        var locked = converted.LockBits(new Rectangle(0, 0, converted.Width, converted.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[locked.Stride * converted.Height];
            Marshal.Copy(locked.Scan0, bytes, 0, bytes.Length);
            var colors = new int[converted.Width * converted.Height];
            for (var y = 0; y < converted.Height; y++)
                for (var x = 0; x < converted.Width; x++)
                {
                    var index = y * locked.Stride + x * 4;
                    colors[y * converted.Width + x] = bytes[index + 2] | (bytes[index + 1] << 8) | (bytes[index] << 16);
                }
            return new ScreenAnalysisBuffer(bounds, colors);
        }
        finally { converted.UnlockBits(locked); }
    }

    private Rectangle CheckedArea(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0 || !Bounds.Contains(area))
            throw new InvalidOperationException($"Область {area} выходит за снимок {Bounds}. Обновите буфер GETSCREEN.");
        return area;
    }

    private int Index(int x, int y) => (y - Bounds.Top) * Bounds.Width + x - Bounds.Left;
    private static int Ref(Color color) => color.R | (color.G << 8) | (color.B << 16);
    private static bool Match(int actual, int expected, int tolerance) =>
        Math.Abs((actual & 255) - (expected & 255)) <= tolerance &&
        Math.Abs(((actual >> 8) & 255) - ((expected >> 8) & 255)) <= tolerance &&
        Math.Abs(((actual >> 16) & 255) - ((expected >> 16) & 255)) <= tolerance;

    public int Pixel(Point point)
    {
        if (!Bounds.Contains(point)) throw new InvalidOperationException($"Точка {point} вне снимка {Bounds}.");
        lock (_gate) return _colors[Index(point.X, point.Y)];
    }

    public int Count(Rectangle area, Color color, int tolerance, CancellationToken cancel)
    {
        CheckedArea(area);
        tolerance = Math.Clamp(tolerance, 0, 255);
        var wanted = Ref(color);
        var count = 0;
        lock (_gate)
            for (var y = area.Top; y < area.Bottom; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x < area.Right; x++)
                    if (Match(_colors[Index(x, y)], wanted, tolerance)) count++;
            }
        return count;
    }

    public Point? Find(Rectangle area, Color color, int tolerance, CancellationToken cancel)
    {
        CheckedArea(area);
        tolerance = Math.Clamp(tolerance, 0, 255);
        var wanted = Ref(color);
        lock (_gate)
            for (var y = area.Top; y < area.Bottom; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x < area.Right; x++)
                    if (Match(_colors[Index(x, y)], wanted, tolerance)) return new Point(x, y);
            }
        return null;
    }

    public List<Point> ScanColor(Rectangle area, Color color, int tolerance, int maxResults, CancellationToken cancel)
    {
        CheckedArea(area);
        if (maxResults is < 1 or > 10000) throw new InvalidOperationException("Предел результатов должен быть от 1 до 10000.");
        tolerance = Math.Clamp(tolerance, 0, 255);
        var wanted = Ref(color);
        var results = new List<Point>();
        lock (_gate)
            for (var y = area.Top; y < area.Bottom; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x < area.Right; x++)
                    if (Match(_colors[Index(x, y)], wanted, tolerance))
                    {
                        results.Add(new Point(x, y));
                        if (results.Count == maxResults) return results;
                    }
            }
        return results;
    }

    public List<Point> ScanImage(Rectangle area, string path, int tolerance, Color? ignoredColor,
        int maxResults, CancellationToken cancel)
    {
        CheckedArea(area);
        if (maxResults is < 1 or > 10000) throw new InvalidOperationException("Предел результатов должен быть от 1 до 10000.");
        tolerance = Math.Clamp(tolerance, 0, 255);
        using var source = new Bitmap(path);
        if (source.Width > area.Width || source.Height > area.Height)
            return [];
        if ((long)source.Width * source.Height > 2_000_000)
            throw new InvalidOperationException("Шаблон изображения слишком велик (не более 2 млн пикселей).");
        var template = new List<(int X, int Y, int Color)>();
        var ignored = ignoredColor is Color color ? Ref(color) : -1;
        for (var y = 0; y < source.Height; y++)
        {
            cancel.ThrowIfCancellationRequested();
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (pixel.A == 0) continue;
                var reference = Ref(pixel);
                if (reference != ignored) template.Add((x, y, reference));
            }
        }
        if (template.Count == 0)
            throw new InvalidOperationException("Все пиксели шаблона прозрачны или исключены цветом фона.");
        var results = new List<Point>();
        lock (_gate)
            for (var y = area.Top; y <= area.Bottom - source.Height; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x <= area.Right - source.Width; x++)
                {
                    var matched = true;
                    foreach (var sample in template)
                        if (!Match(_colors[Index(x + sample.X, y + sample.Y)], sample.Color, tolerance))
                        { matched = false; break; }
                    if (!matched) continue;
                    results.Add(new Point(x, y));
                    if (results.Count == maxResults) return results;
                }
            }
        return results;
    }

    public uint Hash(Rectangle area, bool crc32, CancellationToken cancel)
    {
        CheckedArea(area);
        uint value = crc32 ? 0xffffffff : 0;
        lock (_gate)
            for (var y = area.Top; y < area.Bottom; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x < area.Right; x++)
                {
                    var color = _colors[Index(x, y)];
                    if (!crc32) { value ^= (uint)color; continue; }
                    for (var channel = 0; channel < 3; channel++)
                    {
                        value ^= (byte)(color >> (channel * 8));
                        for (var bit = 0; bit < 8; bit++)
                            value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0u);
                    }
                }
            }
        return crc32 ? ~value : value;
    }

    public int Replace(Rectangle area, Color? oldColor, Color newColor, CancellationToken cancel)
    {
        CheckedArea(area);
        var oldRef = oldColor is Color color ? Ref(color) : 0;
        var replacement = Ref(newColor);
        var count = 0;
        lock (_gate)
            for (var y = area.Top; y < area.Bottom; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (var x = area.Left; x < area.Right; x++)
                {
                    var index = Index(x, y);
                    if (oldColor is not null && _colors[index] != oldRef) continue;
                    _colors[index] = replacement;
                    count++;
                }
            }
        return count;
    }

    public void SavePng(string path)
    {
        using var bitmap = new Bitmap(Bounds.Width, Bounds.Height, PixelFormat.Format32bppArgb);
        var locked = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[locked.Stride * bitmap.Height];
            lock (_gate)
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var color = _colors[y * bitmap.Width + x];
                        var index = y * locked.Stride + x * 4;
                        bytes[index] = (byte)(color >> 16);
                        bytes[index + 1] = (byte)(color >> 8);
                        bytes[index + 2] = (byte)color;
                        bytes[index + 3] = 255;
                    }
            Marshal.Copy(bytes, 0, locked.Scan0, bytes.Length);
        }
        finally { bitmap.UnlockBits(locked); }
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null) Directory.CreateDirectory(directory);
        bitmap.Save(path, ImageFormat.Png);
    }
}
