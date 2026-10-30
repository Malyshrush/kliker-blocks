using System.Diagnostics;
using System.Runtime.InteropServices;
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
            return ToNativePath(full);
        }
        var candidates = new[] {
            TesseractInstaller.Executable,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe")
        };
        var found = candidates.FirstOrDefault(File.Exists);
        return found is null ? "tesseract.exe" : ToNativePath(found);
    }

    internal static string ToNativePath(string path)
    {
        if (!OperatingSystem.IsWindows() || path.All(character => character <= 0x7f)) return path;
        var buffer = new StringBuilder(32768);
        var length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
        if (length == 0 || length >= buffer.Capacity) return path;
        var shortPath = buffer.ToString();
        return shortPath.All(character => character <= 0x7f) ? shortPath : path;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);

    internal static async Task<IReadOnlySet<string>> ListLanguagesAsync(string enginePath, CancellationToken cancel)
    {
        var executable = ResolveEngine(enginePath);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add("--list-langs");
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new FileNotFoundException(UiLanguage.T("Tesseract OCR не найден."), executable, error);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var diagnostics = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output;
            var errors = await diagnostics;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("OCR: " + errors.Trim());
            return text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("List of available languages", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(true);
            throw new TimeoutException(UiLanguage.T("Превышен таймаут проверки Tesseract OCR."));
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }

    internal static async Task<string> ReadAsync(string imagePath, string language, string enginePath,
        int timeoutMs, CancellationToken cancel)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("Изображение OCR не найдено.", imagePath);
        if (new FileInfo(imagePath).Length > 32 * 1024 * 1024)
            throw new InvalidOperationException("Изображение OCR больше 32 МиБ.");
        if (!SupportedImageExtensions.Contains(Path.GetExtension(imagePath), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.T("OCR поддерживает PNG, JPEG, BMP, TIFF, GIF и WebP."));
        if (timeoutMs is < 1000 or > 120000)
            throw new InvalidOperationException("Таймаут OCR: 1000–120000 мс.");
        return await ReadInputAsync(ToNativePath(Path.GetFullPath(imagePath)), null, language,
            enginePath, timeoutMs, cancel);
    }

    internal static readonly string[] SupportedImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".webp"];

    internal static Task<string> ReadBytesAsync(byte[] image, string language, string enginePath,
        int timeoutMs, CancellationToken cancel)
    {
        if (image.Length == 0 || image.Length > 32 * 1024 * 1024)
            throw new InvalidOperationException(UiLanguage.T("Изображение OCR должно быть не больше 32 МиБ."));
        return ReadInputAsync("stdin", image, language, enginePath, timeoutMs, cancel);
    }

    private static async Task<string> ReadInputAsync(string input, byte[]? imageBytes, string language,
        string enginePath, int timeoutMs, CancellationToken cancel)
    {
        if (timeoutMs is < 1000 or > 120000)
            throw new InvalidOperationException("Таймаут OCR: 1000–120000 мс.");
        if (string.IsNullOrWhiteSpace(language) || language.Any(ch =>
                !char.IsAsciiLetterOrDigit(ch) && ch != '+' && ch != '_'))
            throw new InvalidOperationException("Код языка OCR содержит недопустимые символы.");
        var executable = ResolveEngine(enginePath);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false,
            RedirectStandardInput = imageBytes is not null,
            RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(input);
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
            if (imageBytes is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(imageBytes, timeout.Token);
                await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
                process.StandardInput.Close();
            }
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
