using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

public sealed class BlockAppearance
{
    public string Shape { get; set; } = "Rounded";
    // Empty means the block category's original color.
    public string FillColor { get; set; } = "";
    public int FillOpacityPercent { get; set; } = 100;
    public string BorderColor { get; set; } = "#424B5A";
    public int BorderOpacityPercent { get; set; } = 100;
    public decimal BorderWidth { get; set; } = 1;
    // Null uses the standard upper-left / lower-right contour position.
    public int? InputAngle { get; set; }
    public Dictionary<string, int> OutputAngles { get; set; } = [];

    public bool IsValid() =>
        Shape is "Rounded" or "Rectangle" or "Square" or "Circle" or "Diamond" &&
        FillColor is not null && (FillColor.Length == 0 || Hex(FillColor)) && Hex(BorderColor) &&
        FillOpacityPercent is >= 0 and <= 100 && BorderOpacityPercent is >= 0 and <= 100 &&
        BorderWidth is >= 1 and <= 12 &&
        (InputAngle is null or >= 0 and <= 359) &&
        OutputAngles is not null && OutputAngles.Values.All(value => value is >= 0 and <= 359);

    private static bool Hex(string value) => value is not null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");

    public BlockAppearance Copy() => new()
    {
        Shape = Shape,
        FillColor = FillColor,
        FillOpacityPercent = FillOpacityPercent,
        BorderColor = BorderColor,
        BorderOpacityPercent = BorderOpacityPercent,
        BorderWidth = BorderWidth,
        InputAngle = InputAngle,
        OutputAngles = new Dictionary<string, int>(OutputAngles)
    };
}

internal static class BlockAppearanceManager
{
    private static Dictionary<string, BlockAppearance> _defaults =
        AppSettings.Load().BlockAppearanceDefaults ?? [];

    internal static BlockAppearance Resolve(BlockNode node)
    {
        if (node.Appearance is { } local && local.IsValid()) return local;
        if (_defaults.TryGetValue(node.Kind, out var global) && global.IsValid()) return global;
        return new BlockAppearance();
    }

    internal static void SaveDefault(string kind, BlockAppearance appearance, AppSettings settings)
    {
        if (!appearance.IsValid()) throw new ArgumentException(nameof(appearance));
        settings.BlockAppearanceDefaults ??= [];
        settings.BlockAppearanceDefaults[kind] = appearance.Copy();
        settings.Save();
        _defaults = settings.BlockAppearanceDefaults.ToDictionary(item => item.Key, item => item.Value.Copy());
    }

    internal static void Reload(AppSettings settings) => _defaults =
        (settings.BlockAppearanceDefaults ?? []).ToDictionary(item => item.Key, item => item.Value.Copy());
}

internal static class BlockGeometry
{
    internal static GraphicsPath Outline(Rectangle bounds, string shape)
    {
        var path = new GraphicsPath();
        switch (shape)
        {
            case "Circle":
                path.AddEllipse(bounds);
                break;
            case "Diamond":
                path.AddPolygon([
                    new Point(bounds.Left + bounds.Width / 2, bounds.Top),
                    new Point(bounds.Right, bounds.Top + bounds.Height / 2),
                    new Point(bounds.Left + bounds.Width / 2, bounds.Bottom),
                    new Point(bounds.Left, bounds.Top + bounds.Height / 2)
                ]);
                break;
            case "Rounded":
                const int radius = 9;
                var diameter = radius * 2;
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                break;
            default:
                path.AddRectangle(bounds);
                break;
        }
        return path;
    }

    internal static int DefaultCornerAngle(Rectangle bounds) =>
        (int)Math.Round(Math.Atan2(bounds.Height / 2d, bounds.Width / 2d) * 180 / Math.PI);

    internal static Point OnContour(Rectangle bounds, string shape, int angle)
    {
        var radians = angle * Math.PI / 180;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);
        var rx = bounds.Width / 2d;
        var ry = bounds.Height / 2d;
        var t = shape switch
        {
            "Circle" => 1 / Math.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry)),
            "Diamond" => 1 / (Math.Abs(dx) / rx + Math.Abs(dy) / ry),
            _ => Math.Min(Math.Abs(dx) < 1e-8 ? double.PositiveInfinity : rx / Math.Abs(dx),
                          Math.Abs(dy) < 1e-8 ? double.PositiveInfinity : ry / Math.Abs(dy))
        };
        if (shape == "Rounded")
        {
            var low = 0d;
            var high = t;
            for (var i = 0; i < 20; i++)
            {
                var mid = (low + high) / 2;
                if (InsideRounded(bounds, bounds.Left + rx + dx * mid, bounds.Top + ry + dy * mid)) low = mid;
                else high = mid;
            }
            t = low;
        }
        return new Point((int)Math.Round(bounds.Left + rx + dx * t),
            (int)Math.Round(bounds.Top + ry + dy * t));
    }

    private static bool InsideRounded(Rectangle bounds, double x, double y)
    {
        const double radius = 9;
        var clampedX = Math.Clamp(x, bounds.Left + radius, bounds.Right - radius);
        var clampedY = Math.Clamp(y, bounds.Top + radius, bounds.Bottom - radius);
        var dx = x - clampedX;
        var dy = y - clampedY;
        return dx * dx + dy * dy <= radius * radius;
    }
}
