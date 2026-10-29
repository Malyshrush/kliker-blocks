using System.Diagnostics;
using System.Text;

namespace KlikerBlocks;

internal static class OcrTools
{
    internal static string ResolveEngine(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
            if (!File.Exists(full)) throw new FileNotFoundException("Tesseract OCR не найден.", full);
            return full;
        }
        var candidates = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "tesseract.exe";
    }

    internal static async Task<string> ReadAsync(string imagePath, string language, string enginePath,
        int timeoutMs, CancellationToken cancel)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("Изображение OCR не найдено.", imagePath);
        if (new FileInfo(imagePath).Length > 32 * 1024 * 1024)
            throw new InvalidOperationException("Изображение OCR больше 32 МиБ.");
        if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }
                .Contains(Path.GetExtension(imagePath), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("OCR поддерживает PNG, JPEG, BMP и TIFF.");
        if (timeoutMs is < 1000 or > 120000)
            throw new InvalidOperationException("Таймаут OCR: 1000–120000 мс.");
        if (string.IsNullOrWhiteSpace(language) || language.Any(ch =>
                !char.IsAsciiLetterOrDigit(ch) && ch != '+' && ch != '_'))
            throw new InvalidOperationException("Код языка OCR содержит недопустимые символы.");
        var executable = ResolveEngine(enginePath);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(imagePath);
        start.ArgumentList.Add("stdout");
        start.ArgumentList.Add("-l");
        start.ArgumentList.Add(language);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new InvalidOperationException("Tesseract OCR не установлен. Укажите путь к tesseract.exe в блоке.", error);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(timeoutMs);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var diagnostics = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output;
            var errors = await diagnostics;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("OCR: " + errors.Trim());
            if (text.Length > 100000)
                throw new InvalidOperationException("Результат OCR длиннее 100000 символов.");
            return text.Trim();
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(true);
            throw new TimeoutException("Превышен таймаут OCR.");
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
}
