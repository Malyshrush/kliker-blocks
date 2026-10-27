using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class TrayStatusIcon
{
    public static Icon Create(Icon baseIcon, Color statusColor)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawIcon(baseIcon, new Rectangle(0, 0, 16, 16));
            using var border = new SolidBrush(Color.White);
            using var fill = new SolidBrush(statusColor);
            graphics.FillEllipse(border, 7, 7, 9, 9);
            graphics.FillEllipse(fill, 9, 9, 6, 6);
        }

        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);
}
