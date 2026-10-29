using System.Drawing;

namespace KlikerBlocks;

internal static class LinkSelectionGeometry
{
    internal static double HitRadius(float zoom, float thickness) =>
        Math.Max(14 / Math.Max(0.05f, zoom), thickness / 2 + 4 / Math.Max(0.05f, zoom));

    internal static double SegmentDistance(Point point, Point start, Point end)
        => SegmentDistance(point, (PointF)start, end);

    internal static double SegmentDistance(Point point, PointF start, PointF end)
    {
        var dx = (double)end.X - start.X;
        var dy = (double)end.Y - start.Y;
        var square = dx * dx + dy * dy;
        if (square < 0.001) return Math.Sqrt(Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2));
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / square, 0, 1);
        var px = start.X + t * dx;
        var py = start.Y + t * dy;
        return Math.Sqrt(Math.Pow(point.X - px, 2) + Math.Pow(point.Y - py, 2));
    }

    internal static PointF Midpoint(IReadOnlyList<Point> route)
        => Midpoint(route.Select(point => (PointF)point).ToArray());

    internal static PointF Midpoint(IReadOnlyList<PointF> route)
    {
        if (route.Count == 0) return PointF.Empty;
        if (route.Count == 1) return route[0];
        var lengths = new double[route.Count - 1];
        for (var index = 1; index < route.Count; index++)
        {
            var dx = (double)route[index].X - route[index - 1].X;
            var dy = (double)route[index].Y - route[index - 1].Y;
            lengths[index - 1] = Math.Sqrt(dx * dx + dy * dy);
        }
        var remaining = lengths.Sum() / 2;
        for (var index = 1; index < route.Count; index++)
        {
            var length = lengths[index - 1];
            if (remaining <= length || index == route.Count - 1)
            {
                var fraction = length < 0.001 ? 0 : remaining / length;
                return new PointF((float)(route[index - 1].X + fraction * (route[index].X - route[index - 1].X)),
                    (float)(route[index - 1].Y + fraction * (route[index].Y - route[index - 1].Y)));
            }
            remaining -= length;
        }
        return route[^1];
    }
}
