using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;

namespace KlikerBlocks;

internal static class BarcodeTools
{
    internal static string Read(Bitmap image)
    {
        if ((long)image.Width * image.Height > 30_000_000)
            throw new ArgumentException(UiLanguage.Code == "ru" ? "Изображение слишком велико для распознавания." : "Image is too large to decode.");
        using var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(copy)) { graphics.Clear(Color.White); graphics.DrawImageUnscaled(image, 0, 0); }
        var data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[copy.Width * copy.Height * 4];
            for (int y = 0; y < copy.Height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * copy.Width * 4, copy.Width * 4);
            var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new ZXing.Common.DecodingOptions { TryHarder = true } };
            return reader.Decode(bytes, copy.Width, copy.Height, RGBLuminanceSource.BitmapFormat.BGRA32)?.Text ?? "";
        }
        finally { copy.UnlockBits(data); }
    }
}
