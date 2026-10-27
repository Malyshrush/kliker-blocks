using System.Windows.Automation;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class PaintShowcaseTools
{
    internal readonly record struct ToolSelection(bool PencilSelected, bool RedSelected);

    internal static ToolSelection SelectPencilAndRed(IntPtr window)
    {
        var pencilSelected = false;
        var redSelected = false;
        try
        {
            var root = AutomationElement.FromHandle(window);
            var pencil = root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "PencilTool"));
            // Classic Paint may expose only a localized name, not the newer automation id.
            if (pencil is null)
            {
                var tools = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                for (var i = 0; i < tools.Count; i++)
                    if (KnownPencilName(tools[i].Current.Name))
                    { pencil = tools[i]; break; }
            }
            if (pencil is not null)
            {
                ClickElement(pencil);
                pencilSelected = true;
            }
            else
            {
                // In current Paint the Pencil lives in a collapsed Tools menu.
                // Brushes is exposed as a separate, directly selectable tool.
                var controls = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                for (var i = 0; i < controls.Count; i++)
                {
                    var current = controls[i].Current;
                    if (!KnownBrushName(current.Name) &&
                        !KnownBrushName(current.AutomationId)) continue;
                    if (current.BoundingRectangle.IsEmpty) continue;
                    ClickElement(controls[i]);
                    pencilSelected = true;
                    break;
                }
            }
            var redNames = new[] { "Красный", "Red", "Rouge", "Rojo", "Rot", "红色", "紅色" };
            var colors = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            for (var i = 0; i < colors.Count; i++)
            {
                if (!redNames.Contains(colors[i].Current.Name, StringComparer.OrdinalIgnoreCase)) continue;
                ClickElement(colors[i]);
                redSelected = true;
                break;
            }
        }
        catch (Exception error) when (error is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // Current Paint builds do not always expose drawing tools through UI Automation.
            // A newly opened blank Paint usually has a usable brush selected already.
        }
        if (!redSelected)
        {
            // Older Paint exposes owner-drawn color cells with no accessible name.
            var bounds = WindowTools.Bounds(window);
            using var image = ScreenTools.Capture(bounds);
            if (FindRedSwatch(image) is { } swatch)
            {
                NativeMouse.MoveInjected(new Point(bounds.Left + swatch.X, bounds.Top + swatch.Y));
                NativeMouse.MouseDownInjected();
                NativeMouse.MouseUpInjected();
                redSelected = true;
            }
        }
        return new ToolSelection(pencilSelected, redSelected);
    }

    internal static string FallbackNotice(bool pencil) => (UiLanguage.Code, pencil) switch
    {
        ("en", true) => "Paint did not expose Pencil; trying the current drawing tool and verifying the strokes.",
        ("fr", true) => "Paint n'expose pas le Crayon ; essai de l'outil actif avec vérification des traits.",
        ("es", true) => "Paint no expone el Lápiz; se probará la herramienta actual y se verificarán los trazos.",
        ("zh", true) => "画图未公开铅笔工具；将尝试当前绘图工具并验证笔迹。",
        ("de", true) => "Paint stellt den Stift nicht bereit; das aktuelle Zeichenwerkzeug wird mit Strichprüfung versucht.",
        (_, true) => "Paint не показал «Карандаш»; пробую текущий инструмент и проверю штрихи.",
        ("en", false) => "Paint did not expose red; drawing with the current color.",
        ("fr", false) => "Paint n'expose pas le rouge ; dessin avec la couleur actuelle.",
        ("es", false) => "Paint no expone el rojo; se dibujará con el color actual.",
        ("zh", false) => "画图未公开红色；将使用当前颜色绘制。",
        ("de", false) => "Paint stellt Rot nicht bereit; es wird mit der aktuellen Farbe gezeichnet.",
        _ => "Paint не показал красный цвет; рисую текущим цветом."
    };

    internal static bool KnownPencilName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return new[] { "Карандаш", "Pencil", "Crayon", "Lápiz", "Bleistift", "铅笔" }
            .Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool KnownBrushName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return new[] { "Кисти", "Кисть", "Brushes", "Brush", "Pinceaux", "Pincel", "Pinceles", "Pinsel", "画笔" }
            .Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // Reset by undoing our own mouse strokes; selecting/deleting canvas pixels
    // changes the active tool in modern Paint and breaks the second drawing.
    internal static IReadOnlyList<byte> ResetKeys(int completedStrokes) =>
        Enumerable.Repeat((byte)0x5A, Math.Clamp(completedStrokes, 0, 32)).ToArray();

    private static void ClickElement(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
        {
            ((InvokePattern)pattern).Invoke();
            return;
        }
        var bounds = element.Current.BoundingRectangle;
        if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2)
            throw new InvalidOperationException(UiLanguage.T("Paint: инструмент недоступен на экране."));
        NativeMouse.MoveInjected(new Point((int)(bounds.Left + bounds.Width / 2),
            (int)(bounds.Top + bounds.Height / 2)));
        NativeMouse.MouseDownInjected();
        NativeMouse.MouseUpInjected();
    }

    internal static Rectangle LocateWhiteCanvas(Bitmap bitmap, Point screenOrigin)
    {
        Rectangle best = Rectangle.Empty;
        for (var y = bitmap.Height / 3; y < bitmap.Height * 9 / 10; y += Math.Max(12, bitmap.Height / 12))
            for (var x = bitmap.Width / 4; x < bitmap.Width * 4 / 5; x += Math.Max(12, bitmap.Width / 12))
            {
                if (!IsWhite(bitmap.GetPixel(x, y))) continue;
                var region = WhiteRegion(bitmap, x, y);
                if (region.Width >= 280 && region.Height >= 240 &&
                    region.Width * region.Height > best.Width * best.Height)
                    best = region;
            }
        if (best.IsEmpty)
            throw new InvalidOperationException(UiLanguage.T("Paint: пустой белый холст не найден."));
        return Rectangle.Inflate(new Rectangle(screenOrigin.X + best.Left, screenOrigin.Y + best.Top,
            best.Width, best.Height), -24, -24);
    }

    private static Rectangle WhiteRegion(Bitmap bitmap, int centerX, int centerY)
    {
        var left = centerX;
        var right = centerX;
        var top = centerY;
        var bottom = centerY;
        while (left > 0 && IsWhite(bitmap.GetPixel(left - 1, centerY))) left--;
        while (right + 1 < bitmap.Width && IsWhite(bitmap.GetPixel(right + 1, centerY))) right++;
        while (top > 0 && IsWhite(bitmap.GetPixel(centerX, top - 1))) top--;
        while (bottom + 1 < bitmap.Height && IsWhite(bitmap.GetPixel(centerX, bottom + 1))) bottom++;
        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    internal static Point? FindRedSwatch(Bitmap bitmap)
    {
        static bool Red(Color color) => color.R >= 185 && color.G <= 95 && color.B <= 95 &&
            color.R >= color.G * 2 && color.R >= color.B * 2;
        for (var y = 10; y < bitmap.Height - 12; y += 2)
        {
            if (y > bitmap.Height * 2 / 5 && y < bitmap.Height * 4 / 5) continue;
            for (var x = 12; x < bitmap.Width - 12; x += 2)
            {
                if (!Red(bitmap.GetPixel(x, y))) continue;
                var solid = true;
                for (var dy = -3; dy <= 3 && solid; dy += 3)
                    for (var dx = -3; dx <= 3; dx += 3)
                        if (!Red(bitmap.GetPixel(x + dx, y + dy))) { solid = false; break; }
                if (solid) return new Point(x, y);
            }
        }
        return null;
    }

    internal static bool HasVisibleChange(Bitmap before, Bitmap after)
    {
        if (before.Size != after.Size) return false;
        var changed = 0;
        for (var y = 0; y < before.Height; y += 2)
            for (var x = 0; x < before.Width; x += 2)
            {
                var first = before.GetPixel(x, y);
                var second = after.GetPixel(x, y);
                if (Math.Abs(first.R - second.R) + Math.Abs(first.G - second.G) +
                    Math.Abs(first.B - second.B) > 80 && ++changed >= 20) return true;
            }
        return false;
    }

    private static bool IsWhite(Color color) => color.R >= 245 && color.G >= 245 && color.B >= 245;

    internal static List<Point[]> Strokes(string shape, Rectangle canvas)
    {
        var center = new Point(canvas.Left + canvas.Width / 2, canvas.Top + canvas.Height / 2);
        var radius = Math.Min(canvas.Width * 0.28f, canvas.Height * 0.42f);
        Point At(float x, float y) => new(center.X + (int)Math.Round(x * radius),
            center.Y + (int)Math.Round(y * radius));
        Point[] Line(params (float X, float Y)[] vertices)
        {
            var points = new List<Point>();
            foreach (var vertex in vertices)
            {
                var next = At(vertex.X, vertex.Y);
                if (points.Count == 0) { points.Add(next); continue; }
                var prior = points[^1];
                var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(
                    Math.Pow(next.X - prior.X, 2) + Math.Pow(next.Y - prior.Y, 2)) / 4));
                for (var i = 1; i <= steps; i++)
                    points.Add(new Point(prior.X + (next.X - prior.X) * i / steps,
                        prior.Y + (next.Y - prior.Y) * i / steps));
            }
            return points.ToArray();
        }
        if (shape == "arrow") return
        [
            Line((0.9f, 0), (-0.85f, 0)),
            Line((-0.25f, -0.55f), (-0.85f, 0), (-0.25f, 0.55f))
        ];
        if (shape == "house") return
        [
            Line((-0.9f, -0.1f), (0, -0.85f), (0.9f, -0.1f)),
            Line((-0.7f, -0.1f), (-0.7f, 0.85f), (0.7f, 0.85f), (0.7f, -0.1f)),
            Line((-0.18f, 0.85f), (-0.18f, 0.2f), (0.18f, 0.2f), (0.18f, 0.85f))
        ];
        if (shape == "heart")
        {
            var points = new List<Point>();
            void Curve((float X, float Y) a, (float X, float Y) b,
                (float X, float Y) c, (float X, float Y) d)
            {
                for (var i = 0; i <= 45; i++)
                {
                    var t = i / 45f;
                    var u = 1 - t;
                    points.Add(At(u*u*u*a.X + 3*u*u*t*b.X + 3*u*t*t*c.X + t*t*t*d.X,
                        u*u*u*a.Y + 3*u*u*t*b.Y + 3*u*t*t*c.Y + t*t*t*d.Y));
                }
            }
            Curve((0, 0.9f), (-1.2f, 0), (-1.1f, -1.05f), (-0.4f, -0.8f));
            Curve((-0.4f, -0.8f), (-0.12f, -0.72f), (0, -0.48f), (0, -0.36f));
            Curve((0, -0.36f), (0, -0.48f), (0.12f, -0.72f), (0.4f, -0.8f));
            Curve((0.4f, -0.8f), (1.1f, -1.05f), (1.2f, 0), (0, 0.9f));
            return [points.ToArray()];
        }
        var star = new List<(float X, float Y)>();
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var r = i % 2 == 0 ? 0.95f : 0.4f;
            star.Add(((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r));
        }
        star.Add(star[0]);
        return [Line(star.ToArray())];
    }
}
