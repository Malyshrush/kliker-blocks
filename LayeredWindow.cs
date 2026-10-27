using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class LayeredWindow
{
    private const int UlwAlpha = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int width, int height)
    {
        public int Width = width;
        public int Height = height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
        public uint ColorTable;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr bitmap);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr bitmap);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage,
        out IntPtr bits, IntPtr section, uint offset);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
        ref NativePoint destination, ref NativeSize size, IntPtr sourceDC, ref NativePoint source,
        int colorKey, ref BlendFunction blend, int flags);

    public static void Update(IntPtr window, Point location, Bitmap bitmap)
    {
        var screenDC = GetDC(IntPtr.Zero);
        if (screenDC == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var memoryDC = IntPtr.Zero;
        var nativeBitmap = IntPtr.Zero;
        var former = IntPtr.Zero;
        try
        {
            memoryDC = CreateCompatibleDC(screenDC);
            if (memoryDC == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new BitmapInfo
            {
                Size = 40, Width = bitmap.Width, Height = -bitmap.Height,
                Planes = 1, BitCount = 32, Compression = 0,
                ImageSize = (uint)(bitmap.Width * bitmap.Height * 4)
            };
            nativeBitmap = CreateDIBSection(screenDC, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (nativeBitmap == IntPtr.Zero || bits == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var pixels = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[bitmap.Width * 4];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(pixels.Scan0, y * pixels.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(bits, y * row.Length), row.Length);
                }
            }
            finally { bitmap.UnlockBits(pixels); }
            former = SelectObject(memoryDC, nativeBitmap);
            var destination = new NativePoint(location.X, location.Y);
            var source = new NativePoint(0, 0);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0,
                SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(window, screenDC, ref destination, ref size, memoryDC,
                    ref source, 0, ref blend, UlwAlpha))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (former != IntPtr.Zero && memoryDC != IntPtr.Zero) SelectObject(memoryDC, former);
            if (nativeBitmap != IntPtr.Zero) DeleteObject(nativeBitmap);
            if (memoryDC != IntPtr.Zero) DeleteDC(memoryDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }
    }
}
