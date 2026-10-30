using System.Diagnostics;

namespace KlikerBlocks;

internal static class TesseractInstaller
{
    // The Tesseract GitHub release hosts this current Windows build; Tesseract documentation
    // also identifies UB Mannheim as a source of Windows installers.
    internal const string InstallerUrl =
        "https://github.com/tesseract-ocr/tesseract/releases/download/5.5.0/tesseract-ocr-w64-setup-5.5.0.20241111.exe";
    internal const string ModelBaseUrl =
        "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/";
    internal static string Root => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "KLIKER Blocks", "Tesseract-OCR");
    internal static string Executable => Path.Combine(Root, "tesseract.exe");

    private static string L(string ru, string en) => UiLanguage.Code == "en" ? en : ru;

    internal static IReadOnlyList<string> ParseLanguages(string value)
    {
        var languages = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (languages.Length == 0 || languages.Any(language => language.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_')))
            throw new InvalidOperationException(L("Укажите код языка Tesseract, например eng+rus.",
                "Enter a Tesseract language code, for example eng+rus."));
        return languages;
    }

    internal static async Task<string> InstallAsync(string languages, IProgress<string> progress,
        CancellationToken cancel)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
            throw new InvalidOperationException(L(
                "Автоматическая установка Tesseract доступна только на 64-разрядной Windows. Укажите путь к установленному tesseract.exe вручную.",
                "Automatic Tesseract setup is available only on 64-bit Windows. Enter the path to an installed tesseract.exe manually."));
        var required = ParseLanguages(languages);
        if (!File.Exists(Executable))
        {
            Directory.CreateDirectory(Root);
            var installer = Path.Combine(Root, "Downloads", Path.GetFileName(new Uri(InstallerUrl).LocalPath));
            try
            {
                progress.Report(L("Загрузка Tesseract OCR с сервера UB Mannheim…",
                    "Downloading Tesseract OCR from UB Mannheim…"));
                using var client = CreateClient();
                await DownloadAsync(client, new Uri(InstallerUrl), installer, 250L * 1024 * 1024, progress, cancel);
                progress.Report(L("Установка Tesseract OCR для текущего пользователя…",
                    "Installing Tesseract OCR for the current user…"));
                await RunProcessAsync(installer, ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-",
                    "/NOSTARTMENU", "/NOICONS", "/DIR=" + Root], TimeSpan.FromMinutes(15), cancel);
            }
            finally
            {
                if (File.Exists(installer)) File.Delete(installer);
            }
        }
        if (!File.Exists(Executable))
            throw new InvalidOperationException(L("Установка завершилась, но tesseract.exe не найден: ",
                "Setup finished but tesseract.exe was not found: ") + Executable);

        var installed = await OcrTools.ListLanguagesAsync(Executable, cancel);
        using var modelClient = CreateClient();
        foreach (var language in required.Where(language => !installed.Contains(language)))
        {
            var destination = Path.Combine(Root, "tessdata", language + ".traineddata");
            progress.Report(L($"Загрузка языковой модели OCR: {language}…",
                $"Downloading OCR language model: {language}…"));
            await DownloadAsync(modelClient, new Uri(ModelBaseUrl + Uri.EscapeDataString(language) + ".traineddata"),
                destination, 100L * 1024 * 1024, progress, cancel);
        }
        installed = await OcrTools.ListLanguagesAsync(Executable, cancel);
        var missing = required.Where(language => !installed.Contains(language)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(L("Не удалось установить языковые данные Tesseract: ",
                "Could not install Tesseract language data: ") + string.Join(", ", missing));
        progress.Report(L("Tesseract OCR готов. Сценарий продолжится без перезапуска.",
            "Tesseract OCR is ready. The scenario continues without a restart."));
        return Executable;
    }

    private static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromMinutes(15) };

    private static async Task DownloadAsync(HttpClient client, Uri url, string destination, long maximum,
        IProgress<string> progress, CancellationToken cancel)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long total && total > maximum)
            throw new InvalidDataException(L("Размер загрузки превышает допустимый предел.",
                "Download exceeds the allowed size."));
        await using var input = await response.Content.ReadAsStreamAsync(cancel);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[128 * 1024];
        var written = 0L;
        var nextReport = 4L * 1024 * 1024;
        int read;
        while ((read = await input.ReadAsync(buffer, cancel)) > 0)
        {
            written += read;
            if (written > maximum) throw new InvalidDataException(L("Размер загрузки превышает допустимый предел.",
                "Download exceeds the allowed size."));
            await output.WriteAsync(buffer.AsMemory(0, read), cancel);
            if (written >= nextReport)
            {
                progress.Report(L($"Загружено {written / 1024d / 1024d:0.0} МиБ…",
                    $"Downloaded {written / 1024d / 1024d:0.0} MiB…"));
                nextReport += 4L * 1024 * 1024;
            }
        }
    }

    private static async Task RunProcessAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancel)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException(L(
            "Установщик Tesseract не запустился.", "The Tesseract installer did not start."));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(timeout);
        try
        {
            var errors = process.StandardError.ReadToEndAsync(deadline.Token);
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            var details = await errors;
            if (details.Length == 0) details = await output;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(L($"Установщик Tesseract завершился с кодом {process.ExitCode}.",
                    $"Tesseract installer exited with code {process.ExitCode}.") +
                    (details.Length == 0 ? "" : " " + details[..Math.Min(600, details.Length)]));
        }
        finally
        {
            if (!process.HasExited) try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
        }
    }
}
