using System.Diagnostics;
using System.Text;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;

namespace KlikerBlocks;

internal static class PdfOcrTools
{
    private const int MaxAllPages = 20;
    private const int MaxOutputLength = 100000;

    internal static async Task<string> ReadAsync(string path, int requestedPage, string language,
        string enginePath, int timeoutMs, CancellationToken cancel)
    {
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.T("Для OCR PDF-файла ожидается расширение .pdf."));
        if (!File.Exists(path)) throw new FileNotFoundException(UiLanguage.T("PDF-файл для OCR не найден."), path);
        if (new FileInfo(path).Length > 32 * 1024 * 1024)
            throw new InvalidOperationException(UiLanguage.T("PDF-файл для OCR больше 32 МиБ."));
        if (requestedPage is < 0 or > 100)
            throw new InvalidOperationException(UiLanguage.T("Страница PDF для OCR вне диапазона."));
        if (timeoutMs is < 1000 or > 120000)
            throw new InvalidOperationException("Таймаут OCR: 1000–120000 мс.");

        using var document = PdfDocument.Open(path);
        var pageCount = document.NumberOfPages;
        if (requestedPage > pageCount)
            throw new InvalidOperationException(UiLanguage.T("Страница PDF для OCR вне диапазона."));
        if (requestedPage == 0 && pageCount > MaxAllPages)
            throw new InvalidOperationException(UiLanguage.T("Для OCR всех страниц допускается не более 20 страниц."));

        var pdf = await File.ReadAllBytesAsync(path, cancel);
        IEnumerable<int> pages = requestedPage == 0 ? Enumerable.Range(1, pageCount) : [requestedPage];
        var output = new StringBuilder();
        var timer = Stopwatch.StartNew();
        var options = new RenderOptions { Dpi = 200, Grayscale = true, UseTiling = true };
        foreach (var page in pages)
        {
            cancel.ThrowIfCancellationRequested();
            if (timer.ElapsedMilliseconds >= timeoutMs)
                throw new TimeoutException(UiLanguage.T("Таймаут OCR по PDF превышен."));
            var png = await Task.Run(() => RenderPage(pdf, page - 1, options), cancel);
            var remaining = timeoutMs - (int)timer.ElapsedMilliseconds;
            if (remaining < 1000)
                throw new TimeoutException(UiLanguage.T("Таймаут OCR по PDF превышен."));
            var text = await OcrTools.ReadBytesAsync(png, language, enginePath,
                Math.Min(120000, remaining), cancel);
            if (output.Length > 0) output.Append("\n\f\n");
            output.Append(text);
            if (output.Length > MaxOutputLength)
                throw new InvalidOperationException("Результат OCR длиннее 100000 символов.");
        }
        return output.ToString().Trim();
    }

    internal static byte[] RenderPage(byte[] pdf, int zeroBasedPage) =>
        RenderPage(pdf, zeroBasedPage, new RenderOptions { Dpi = 200, Grayscale = true, UseTiling = true });

    private static byte[] RenderPage(byte[] pdf, int zeroBasedPage, RenderOptions options)
    {
        using var bitmap = Conversion.ToImage(pdf, new Index(zeroBasedPage), options: options);
        using var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        if (png.Length > 32 * 1024 * 1024)
            throw new InvalidOperationException(UiLanguage.T("Изображение страницы PDF больше 32 МиБ."));
        return png.ToArray();
    }
}
