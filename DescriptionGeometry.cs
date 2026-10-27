using System.Drawing;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal static class DescriptionGeometry
{
    public const int MinWidth = 220;
    public const int MinHeight = 85;
    public const int MaxSize = 10000;
    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public static Rectangle Bounds(BlockNode node) => new(node.X, node.Y,
        Dimension(node.Value("Width", "700"), 700, MinWidth),
        Dimension(node.Value("Height", "260"), 260, MinHeight));

    public static int Opacity(BlockNode node) => int.TryParse(node.Value("OpacityPercent", "35"), out var value)
        ? Math.Clamp(value, 0, 100) : 35;

    public static Color FillColor(BlockNode node)
    {
        var text = node.Value("FillColor", "#A8D9FF");
        return HexColor.IsMatch(text) ? ColorTranslator.FromHtml(text) : Color.FromArgb(168, 217, 255);
    }

    private static int Dimension(string text, int fallback, int minimum) => int.TryParse(text, out var value)
        ? Math.Clamp(value, minimum, MaxSize) : fallback;
}
