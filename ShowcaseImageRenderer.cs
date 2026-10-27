using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KlikerBlocks;

internal static class ShowcaseImageRenderer
{
    internal const int Width = 996;
    internal const int Height = 634;

    internal static void Render(string shape, string path)
    {
        using var bitmap = new Bitmap(Width, Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);
        graphics.TranslateTransform(Width / 2f, Height / 2f);
        using var outline = new Pen(Color.FromArgb(36, 48, 70), 8)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var accent = new SolidBrush(Color.FromArgb(235, 64, 94));
        using var secondary = new SolidBrush(Color.FromArgb(61, 158, 223));
        switch (shape)
        {
            case "arrow":
                using (var arrow = new Pen(Color.FromArgb(237, 73, 91), 22)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    graphics.DrawLine(arrow, 245, 0, -245, 0);
                    graphics.DrawLines(arrow, [new(-110, -135), new(-245, 0), new(-110, 135)]);
                }
                break;
            case "heart":
                using (var heart = new GraphicsPath())
                {
                    heart.StartFigure();
                    heart.AddBezier(0, 175, -270, -25, -240, -205, -92, -166);
                    heart.AddBezier(-92, -166, -37, -152, -10, -100, 0, -77);
                    heart.AddBezier(0, -77, 10, -100, 37, -152, 92, -166);
                    heart.AddBezier(92, -166, 240, -205, 270, -25, 0, 175);
                    heart.CloseFigure();
                    graphics.FillPath(accent, heart);
                    graphics.DrawPath(outline, heart);
                }
                break;
            case "house":
                var roof = new Point[] { new(-230, -20), new(0, -205), new(230, -20) };
                graphics.FillPolygon(accent, [roof[0], roof[1], roof[2], new(185, 0), new(-185, 0)]);
                graphics.DrawLines(outline, roof);
                graphics.FillRectangle(secondary, -175, -20, 350, 260);
                graphics.DrawRectangle(outline, -175, -20, 350, 260);
                graphics.FillRectangle(Brushes.White, -40, 85, 80, 155);
                graphics.DrawRectangle(outline, -40, 85, 80, 155);
                break;
            default:
                var outer = new PointF[10];
                for (var i = 0; i < outer.Length; i++)
                {
                    var angle = -Math.PI / 2 + i * Math.PI / 5;
                    var radius = i % 2 == 0 ? 225 : 92;
                    outer[i] = new PointF((float)(Math.Cos(angle) * radius),
                        (float)(Math.Sin(angle) * radius));
                }
                graphics.FillPolygon(accent, outer);
                graphics.DrawPolygon(outline, outer);
                break;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
    }
}
