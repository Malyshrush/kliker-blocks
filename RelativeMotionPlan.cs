using System.Drawing;

namespace KlikerBlocks;

internal static class RelativeMotionPlan
{
    public static IReadOnlyList<Point> Split(int deltaX, int deltaY, int steps)
    {
        if (steps is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(steps));
        var result = new Point[steps];
        var previousX = 0;
        var previousY = 0;
        for (var i = 1; i <= steps; i++)
        {
            var x = RoundedCumulative(deltaX, i, steps);
            var y = RoundedCumulative(deltaY, i, steps);
            result[i - 1] = new Point(x - previousX, y - previousY);
            previousX = x;
            previousY = y;
        }
        return result;
    }

    private static int RoundedCumulative(int total, int step, int steps)
    {
        var scaled = (long)total * step;
        return (int)(scaled >= 0
            ? (scaled + steps / 2) / steps
            : -((-scaled + steps / 2) / steps));
    }
}
