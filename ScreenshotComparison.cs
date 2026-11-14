using System.Drawing.Imaging;

namespace KlikerBlocks;

internal static class ScreenshotComparison
{
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;

    internal static Bitmap Compare(Bitmap first, Bitmap second)
    {
        if (first.Size != second.Size)
            throw new ArgumentException(L("Размеры снимков должны совпадать.", "Screenshots must have the same dimensions."));
        if ((long)first.Width * first.Height > 10_000_000)
            throw new ArgumentException(L("Максимальный размер снимка — 10 млн пикселей.", "Maximum image size is 10 million pixels."));
        var result = new Bitmap(first.Width, first.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < first.Height; y++)
        for (int x = 0; x < first.Width; x++)
        {
            var before = first.GetPixel(x, y);
            var after = second.GetPixel(x, y);
            result.SetPixel(x, y, before.ToArgb() == after.ToArgb() ? after : Color.Red);
        }
        return result;
    }

    internal static void Show(IWin32Window owner)
    {
        using var picker = new OpenFileDialog { Filter = L("Изображения|*.png;*.jpg;*.jpeg;*.bmp", "Images|*.png;*.jpg;*.jpeg;*.bmp") };
        picker.Title = L("Выберите первый снимок", "Choose the first screenshot");
        if (picker.ShowDialog(owner) != DialogResult.OK) return;
        string firstPath = picker.FileName;
        picker.Title = L("Выберите второй снимок", "Choose the second screenshot");
        if (picker.ShowDialog(owner) != DialogResult.OK) return;
        try
        {
            using var first = new Bitmap(firstPath);
            using var second = new Bitmap(picker.FileName);
            using var result = Compare(first, second);
            using var window = new Form { Text = L("Различия выделены красным", "Differences are highlighted in red"), Width = 900, Height = 700, StartPosition = FormStartPosition.CenterParent };
            var picture = new PictureBox { Dock = DockStyle.Fill, Image = result, SizeMode = PictureBoxSizeMode.Zoom };
            var save = new Button { Dock = DockStyle.Bottom, Height = 36, Text = L("Сохранить результат…", "Save result…") };
            save.Click += (_, _) =>
            {
                using var dialog = new SaveFileDialog { Filter = "PNG|*.png", DefaultExt = "png", AddExtension = true };
                if (dialog.ShowDialog(window) != DialogResult.OK) return;
                try { result.Save(dialog.FileName, ImageFormat.Png); }
                catch (Exception error) { MessageBox.Show(window, error.Message, L("Ошибка сохранения", "Save error")); }
            };
            window.Controls.Add(picture);
            window.Controls.Add(save);
            window.ShowDialog(owner);
        }
        catch (Exception error) { MessageBox.Show(owner, error.Message, L("Ошибка сравнения", "Comparison error")); }
    }
}
