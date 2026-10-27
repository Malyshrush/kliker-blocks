namespace KlikerBlocks;

internal static class ShowcaseLayout
{
    public static Rectangle NotepadBounds(Rectangle workArea) => new(
        workArea.Left + 16, workArea.Top + 45,
        Math.Min(520, Math.Max(360, workArea.Width / 2 - 65)),
        Math.Min(580, Math.Max(300, workArea.Height - 75)));

    public static Rectangle? DemoBounds(Rectangle workArea, Rectangle notepadBounds, int minimumWidth)
    {
        var left = Math.Max(workArea.Left, notepadBounds.Right + 12);
        return workArea.Right - left < minimumWidth + 12
            ? null
            : new Rectangle(left, workArea.Top + 12, workArea.Right - left - 12,
                workArea.Height - 24);
    }

    public static Point PromptLocation(Rectangle workArea, int width, int height) => new(
        Math.Max(workArea.Left, workArea.Right - width - 26),
        Math.Max(workArea.Top, Math.Min(workArea.Bottom - height - 50, workArea.Top + 110)));
}
