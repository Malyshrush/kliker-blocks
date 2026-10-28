using System.Drawing;

namespace KlikerBlocks;

/// <summary>Routes a connection around executable blocks in world coordinates.</summary>
internal static class GraphLinkRouter
{
    private const int TurnCost = 10;

    internal static int InputAngle(BlockNode node)
    {
        var bounds = BlockCatalog.Bounds(node);
        var style = BlockAppearanceManager.Resolve(node);
        var corner = style.Shape is "Square" or "Circle" or "Diamond"
            ? 45 : BlockGeometry.DefaultCornerAngle(bounds);
        return style.InputAngle ?? 180 + corner;
    }

    internal static int OutputAngle(BlockNode node, int index)
    {
        var bounds = BlockCatalog.Bounds(node);
        var style = BlockAppearanceManager.Resolve(node);
        var output = BlockCatalog.Get(node.Kind).Outputs[index];
        if (style.OutputAngles.TryGetValue(output, out var angle)) return angle;
        var corner = style.Shape is "Square" or "Circle" or "Diamond"
            ? 45 : BlockGeometry.DefaultCornerAngle(bounds);
        return (corner - index * 18 + 360) % 360;
    }

    internal static Point InputPoint(BlockNode node) => BlockGeometry.OnContour(
        BlockCatalog.Bounds(node), BlockAppearanceManager.Resolve(node).Shape, InputAngle(node));

    internal static Point OutputPoint(BlockNode node, int index) => BlockGeometry.OnContour(
        BlockCatalog.Bounds(node), BlockAppearanceManager.Resolve(node).Shape, OutputAngle(node, index));

    internal static Point[] Route(BlockNode source, int outputIndex, BlockNode target,
        IReadOnlyList<BlockNode> nodes, LinkAppearance? appearance = null)
    {
        appearance ??= new LinkAppearance();
        var from = OutputPoint(source, outputIndex);
        var to = InputPoint(target);
        var sourceBounds = BlockCatalog.Bounds(source);
        var targetBounds = BlockCatalog.Bounds(target);
        var sourceExits = OutsideAnchors(from, sourceBounds, OutputAngle(source, outputIndex), appearance.ExitDistance);
        var targetEntries = OutsideAnchors(to, targetBounds, InputAngle(target), appearance.EntryDistance);
        var blocks = nodes.Where(node => node.Kind != "Description")
            .Select(node => (node.Id, Bounds: BlockCatalog.Bounds(node))).ToArray();

        if (appearance.ExitDistance == 0 && appearance.EntryDistance == 0 &&
            PointsOutward(from, to, OutputAngle(source, outputIndex)) &&
            PointsOutward(to, from, InputAngle(target)) &&
            !blocks.Any(block => block.Id != source.Id && block.Id != target.Id &&
                CrossesRectangle(from, to, block.Bounds)))
            return [from, to];

        Point[]? best = null;
        var bestCost = double.PositiveInfinity;
        var corridor = Rectangle.FromLTRB(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y),
            Math.Max(from.X, to.X) + 1, Math.Max(from.Y, to.Y) + 1);
        corridor.Inflate(96, 96);
        var nearby = blocks.Where(item => item.Bounds.IntersectsWith(corridor)).ToArray();
        // Keep ordinary nearby links cheap; broaden the search only if an excluded block
        // would actually be crossed by the proposed route.
        foreach (var candidates in new[] { nearby, blocks })
        {
            foreach (var clearance in new[] { 9, 0 })
            {
                var obstacles = candidates.Select(item => Rectangle.Inflate(item.Bounds, clearance, clearance)).ToArray();
                foreach (var exit in sourceExits)
                {
                    if (CrossesAnotherBlock(from, exit, blocks, source.Id)) continue;
                    foreach (var entry in targetEntries)
                    {
                        if (CrossesAnotherBlock(entry, to, blocks, target.Id)) continue;
                        var middle = RouteCore(exit, entry, obstacles);
                        if (middle == null) continue;
                        var route = Compress([from, .. middle, to]);
                        if (Enumerable.Range(1, route.Length - 1).Any(i => blocks.Any(block =>
                            block.Id != source.Id && block.Id != target.Id &&
                            CrossesInterior(route[i - 1], route[i], block.Bounds)))) continue;
                        var cost = RouteCost(route);
                        if (cost >= bestCost) continue;
                        best = route;
                        bestCost = cost;
                    }
                }
                if (best != null) break;
            }
            if (best != null) break;
        }
        return best ?? [];
    }

    private static bool PointsOutward(Point origin, Point other, int angle)
    {
        var radians = angle * Math.PI / 180;
        return (other.X - origin.X) * Math.Cos(radians) +
            (other.Y - origin.Y) * Math.Sin(radians) >= 0;
    }

    private static Point[] OutsideAnchors(Point point, Rectangle bounds, int angle, int distance)
    {
        var radians = angle * Math.PI / 180;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);
        var result = new List<Point>(2);
        if (Math.Abs(dx) > 1e-6)
            result.Add(new Point(dx > 0 ? bounds.Right + distance : bounds.Left - distance, point.Y));
        if (Math.Abs(dy) > 1e-6)
            result.Add(new Point(point.X, dy > 0 ? bounds.Bottom + distance : bounds.Top - distance));
        return result.ToArray();
    }

    private static bool CrossesRectangle(Point from, Point to, Rectangle rect)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddLine(from, to);
        using var pen = new Pen(Color.Black, 2);
        return path.IsOutlineVisible(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2, pen) ||
            SegmentIntersects(from, to, new(rect.Left, rect.Top), new(rect.Right, rect.Top)) ||
            SegmentIntersects(from, to, new(rect.Right, rect.Top), new(rect.Right, rect.Bottom)) ||
            SegmentIntersects(from, to, new(rect.Right, rect.Bottom), new(rect.Left, rect.Bottom)) ||
            SegmentIntersects(from, to, new(rect.Left, rect.Bottom), new(rect.Left, rect.Top));
    }

    private static bool SegmentIntersects(Point a, Point b, Point c, Point d)
    {
        static long Cross(Point p, Point q, Point r) =>
            (long)(q.X - p.X) * (r.Y - p.Y) - (long)(q.Y - p.Y) * (r.X - p.X);
        var abC = Cross(a, b, c); var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a); var cdB = Cross(c, d, b);
        return (abC < 0 && abD > 0 || abC > 0 && abD < 0) &&
            (cdA < 0 && cdB > 0 || cdA > 0 && cdB < 0);
    }

    private static bool CrossesAnotherBlock(Point start, Point end,
        (Guid Id, Rectangle Bounds)[] blocks, Guid owner)
    {
        foreach (var block in blocks)
            if (block.Id != owner && CrossesInterior(start, end, block.Bounds)) return true;
        return false;
    }

    internal static bool CrossesInterior(Point start, Point end, Rectangle area)
    {
        if (start.X == end.X)
            return start.X > area.Left && start.X < area.Right &&
                Math.Max(start.Y, end.Y) > area.Top && Math.Min(start.Y, end.Y) < area.Bottom;
        if (start.Y == end.Y)
            return start.Y > area.Top && start.Y < area.Bottom &&
                Math.Max(start.X, end.X) > area.Left && Math.Min(start.X, end.X) < area.Right;
        throw new ArgumentException("Graph links must be orthogonal.");
    }

    private static Point[]? RouteCore(Point start, Point end, Rectangle[] obstacles)
    {
        if (InsideAny(start, obstacles) || InsideAny(end, obstacles)) return null;
        var xs = new SortedSet<int> { start.X, end.X };
        var ys = new SortedSet<int> { start.Y, end.Y };
        foreach (var obstacle in obstacles)
        {
            xs.Add(obstacle.Left - 1);
            xs.Add(obstacle.Right + 1);
            ys.Add(obstacle.Top - 1);
            ys.Add(obstacle.Bottom + 1);
        }
        var x = xs.ToArray();
        var y = ys.ToArray();
        var columns = y.Length;
        var stateCount = x.Length * columns * 3;
        var distance = new double[stateCount];
        var previous = new int[stateCount];
        var visited = new bool[stateCount];
        Array.Fill(distance, double.PositiveInfinity);
        Array.Fill(previous, -1);
        int Key(int xi, int yi, int direction) => (xi * columns + yi) * 3 + direction;
        var startKey = Key(Array.BinarySearch(x, start.X), Array.BinarySearch(y, start.Y), 0);
        var goalX = Array.BinarySearch(x, end.X);
        var goalY = Array.BinarySearch(y, end.Y);
        distance[startKey] = 0;
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(startKey, 0);
        var finalKey = -1;
        while (queue.TryDequeue(out var key, out _))
        {
            if (visited[key]) continue;
            visited[key] = true;
            var direction = key % 3;
            var cell = key / 3;
            var xi = cell / columns;
            var yi = cell % columns;
            if (xi == goalX && yi == goalY) { finalKey = key; break; }
            foreach (var (nextX, nextY, nextDirection) in new[]
            {
                (xi - 1, yi, 1), (xi + 1, yi, 1), (xi, yi - 1, 2), (xi, yi + 1, 2)
            })
            {
                if (nextX < 0 || nextX >= x.Length || nextY < 0 || nextY >= y.Length) continue;
                var nextPoint = new Point(x[nextX], y[nextY]);
                if (InsideAny(nextPoint, obstacles)) continue;
                var currentPoint = new Point(x[xi], y[yi]);
                if (obstacles.Any(area => CrossesInterior(currentPoint, nextPoint, area))) continue;
                var nextKey = Key(nextX, nextY, nextDirection);
                var step = Math.Abs(nextPoint.X - currentPoint.X) + Math.Abs(nextPoint.Y - currentPoint.Y);
                var cost = distance[key] + step + (direction != 0 && direction != nextDirection ? TurnCost : 0);
                if (cost >= distance[nextKey]) continue;
                distance[nextKey] = cost;
                previous[nextKey] = key;
                var heuristic = Math.Abs(nextPoint.X - end.X) + Math.Abs(nextPoint.Y - end.Y);
                queue.Enqueue(nextKey, cost + heuristic);
            }
        }
        if (finalKey < 0) return null;
        var result = new List<Point>();
        for (var key = finalKey; key >= 0; key = previous[key])
        {
            var cell = key / 3;
            result.Add(new Point(x[cell / columns], y[cell % columns]));
        }
        result.Reverse();
        return Compress(result);
    }

    private static bool InsideAny(Point point, Rectangle[] obstacles) => obstacles.Any(area =>
        point.X > area.Left && point.X < area.Right && point.Y > area.Top && point.Y < area.Bottom);

    private static Point[] Compress(IReadOnlyList<Point> points)
    {
        var result = new List<Point>();
        foreach (var point in points)
        {
            if (result.Count > 0 && result[^1] == point) continue;
            result.Add(point);
            while (result.Count >= 3)
            {
                var count = result.Count;
                var a = result[count - 3];
                var b = result[count - 2];
                var c = result[count - 1];
                if (!(a.X == b.X && b.X == c.X || a.Y == b.Y && b.Y == c.Y)) break;
                result.RemoveAt(count - 2);
            }
        }
        return result.ToArray();
    }

    private static double RouteCost(Point[] points)
    {
        double cost = 0;
        for (var i = 1; i < points.Length; i++)
        {
            cost += Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y);
            if (i >= 2 && (points[i].X == points[i - 1].X) !=
                (points[i - 1].X == points[i - 2].X)) cost += TurnCost;
        }
        return cost;
    }
}
