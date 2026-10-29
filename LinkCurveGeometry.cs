using System.Drawing;

namespace KlikerBlocks;

/// <summary>Display and hit-test geometry for independently rounded link ends.</summary>
internal static class LinkCurveGeometry
{
    internal static PointF[] Build(IReadOnlyList<Point> route, LinkAppearance appearance,
        IReadOnlyList<BlockNode> nodes)
    {
        if (route.Count < 2) return [];
        var points = new List<PointF> { route[0] };
        var obstacles = nodes.Where(node => node.Kind != "Description")
            .Select(node => BlockCatalog.Bounds(node)).ToArray();
        for (var i = 1; i < route.Count - 1; i++)
        {
            var previous = route[i - 1];
            var corner = route[i];
            var next = route[i + 1];
            var incoming = Unit(previous, corner);
            var outgoing = Unit(corner, next);
            if (incoming == outgoing || incoming == PointF.Empty || outgoing == PointF.Empty)
            {
                Add(points, corner);
                continue;
            }
            var configured = i == 1 ? appearance.ExitRadius :
                i == route.Count - 2 ? appearance.EntryRadius :
                Math.Min(appearance.ExitRadius, appearance.EntryRadius);
            var before = Distance(previous, corner);
            var after = Distance(corner, next);
            var radius = Math.Min(configured, Math.Min(before, after) / 2f);
            PointF start = default, end = default;
            for (; radius >= 1; radius /= 2f)
            {
                start = new PointF(corner.X - incoming.X * radius, corner.Y - incoming.Y * radius);
                end = new PointF(corner.X + outgoing.X * radius, corner.Y + outgoing.Y * radius);
                if (CurveIsClear(start, end, incoming, outgoing, radius,
                    obstacles, (float)appearance.Thickness / 2 + 1)) break;
            }
            if (radius < 1)
            {
                Add(points, corner);
                continue;
            }
            Add(points, start);
            var controls = Controls(start, end, incoming, outgoing, radius);
            var steps = Math.Clamp((int)Math.Ceiling(radius / 2), 12, 100);
            for (var step = 1; step <= steps; step++)
                Add(points, Cubic(start, controls.First, controls.Second, end, step / (float)steps));
        }
        Add(points, route[^1]);
        return points.ToArray();
    }

    private static bool CurveIsClear(PointF start, PointF end,
        PointF incoming, PointF outgoing, float radius, Rectangle[] obstacles, float margin)
    {
        var controls = Controls(start, end, incoming, outgoing, radius);
        var steps = Math.Clamp((int)Math.Ceiling(radius), 16, 200);
        for (var step = 0; step <= steps; step++)
        {
            var point = Cubic(start, controls.First, controls.Second, end, step / (float)steps);
            foreach (var block in obstacles)
                if (point.X > block.Left - margin && point.X < block.Right + margin &&
                    point.Y > block.Top - margin && point.Y < block.Bottom + margin)
                    return false;
        }
        return true;
    }

    private static (PointF First, PointF Second) Controls(PointF start, PointF end,
        PointF incoming, PointF outgoing, float radius)
    {
        const float k = 0.55228475f;
        return (new PointF(start.X + incoming.X * radius * k, start.Y + incoming.Y * radius * k),
            new PointF(end.X - outgoing.X * radius * k, end.Y - outgoing.Y * radius * k));
    }

    private static PointF Cubic(PointF a, PointF b, PointF c, PointF d, float t)
    {
        var u = 1 - t;
        return new PointF(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
            u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }

    private static PointF Unit(Point start, Point end)
    {
        var length = Distance(start, end);
        return length < 0.001f ? PointF.Empty :
            new PointF((end.X - start.X) / length, (end.Y - start.Y) / length);
    }

    private static float Distance(Point a, Point b) =>
        MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static void Add(List<PointF> points, PointF point)
    {
        if (points.Count == 0 || DistanceSquared(points[^1], point) > 0.0001f) points.Add(point);
    }

    private static float DistanceSquared(PointF a, PointF b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
