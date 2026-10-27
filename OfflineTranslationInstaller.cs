using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed record OfflineModelPackage(string Source, string Target, Uri Url);

internal static class OfflineTranslationInstaller
{
    private static string L(string ru, string en) => UiLanguage.Code == "en" ? en : ru;
    internal const string PythonUrl = "https://www.python.org/ftp/python/3.13.17/python-3.13.17-amd64.exe";
    internal const string PythonSha256 = "5da012101ba2e17113a9a1cd901e9ce1441086f2b7ecb5de06ec94163899a190";
    internal const string IndexUrl = "https://raw.githubusercontent.com/argosopentech/argospm-index/main/index.json";
    internal static string Root => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "KLIKER Blocks", "TranslationRuntime");
    internal static string PythonDirectory => Path.Combine(Root, "Python313");
    internal static string PythonExecutable => Path.Combine(PythonDirectory, "python.exe");

    internal static async Task<string> InstallAsync(string source, string target, bool bothDirections,
        IProgress<string> progress, CancellationToken cancel)
    {
        if (!Environment.Is64BitProcess || !OperatingSystem.IsWindows())
            throw new InvalidOperationException(L("Для установки офлайн-перевода нужна 64-разрядная Windows.",
                "Offline translation installer requires 64-bit Windows."));
        if (!TranslationTools.IsLanguage(source) || !TranslationTools.IsLanguage(target) || source == target)
            throw new InvalidOperationException(L("Выберите два разных языка для офлайн-перевода.",
                "Choose two different languages for offline translation."));

        var python = await EnsureEngineAsync(progress, cancel);
        progress.Report("Reading the Argos package catalogue…");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var response = await client.GetAsync(IndexUrl, cancel);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancel);
        if (json.Length > 5_000_000) throw new InvalidDataException(L("Каталог Argos слишком велик.",
            "The Argos catalogue is too large."));
        var available = ReadIndex(json);
        var installed = (await ArgosBridge.ListAsync(python, cancel)).ToList();
        var plan = PlanPackages(available, installed, source, target, bothDirections);
        if (plan.Count == 0) progress.Report("The selected language pair is already installed.");
        foreach (var package in plan)
        {
            cancel.ThrowIfCancellationRequested();
            progress.Report($"Downloading {package.Source} → {package.Target}…");
            var file = Path.Combine(Root, "Downloads", $"{package.Source}_{package.Target}.argosmodel");
            try
            {
                await DownloadAsync(client, package.Url, file, 1024L * 1024 * 1024, cancel);
                var info = ArgosBridge.ReadPackageInfo(file);
                if (info.Source != package.Source || info.Target != package.Target)
                    throw new InvalidDataException(L("Загруженная модель не соответствует выбранной паре языков.",
                        "The downloaded model does not match the selected language pair."));
                progress.Report($"Installing {package.Source} → {package.Target}…");
                await ArgosBridge.InstallFileAsync(python, file, cancel);
                installed.Add(new ArgosModel(package.Source, package.Target, ""));
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
        var current = await ArgosBridge.ListAsync(python, cancel);
        if (!ArgosBridge.CanTranslate(current, source, target) ||
            bothDirections && !ArgosBridge.CanTranslate(current, target, source))
            throw new InvalidOperationException(L("Установленные модели не прошли проверку языковой пары.",
                "The installed models did not pass the language-pair check."));
        progress.Report("Offline translation is ready. Select “Argos local” in a translation block.");
        return python;
    }

    internal static IReadOnlyList<OfflineModelPackage> ReadIndex(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<OfflineModelPackage>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("from_code", out var from) ||
                !item.TryGetProperty("to_code", out var to) ||
                !item.TryGetProperty("links", out var links)) continue;
            var source = from.GetString() ?? "";
            var target = to.GetString() ?? "";
            if (!TranslationTools.IsLanguage(source) || !TranslationTools.IsLanguage(target)) continue;
            foreach (var link in links.EnumerateArray())
            {
                if (!Uri.TryCreate(link.GetString(), UriKind.Absolute, out var url) ||
                    url.Scheme != Uri.UriSchemeHttps ||
                    !url.Host.Equals("argos-net.com", StringComparison.OrdinalIgnoreCase) ||
                    !url.AbsolutePath.EndsWith(".argosmodel", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new OfflineModelPackage(source, target, url));
                break;
            }
        }
        return result;
    }

    internal static IReadOnlyList<OfflineModelPackage> PlanPackages(
        IReadOnlyList<OfflineModelPackage> available, IReadOnlyList<ArgosModel> installed,
        string source, string target, bool bothDirections)
    {
        var plan = new List<OfflineModelPackage>();
        var known = installed.ToList();
        void AddDirection(string from, string to)
        {
            if (ArgosBridge.CanTranslate(known, from, to)) return;
            var direct = available.FirstOrDefault(item => item.Source == from && item.Target == to);
            if (direct is not null) { Add(direct); return; }
            foreach (var (legFrom, legTo) in new[] { (from, "en"), ("en", to) })
            {
                if (legFrom == legTo || ArgosBridge.CanTranslate(known, legFrom, legTo)) continue;
                var leg = available.FirstOrDefault(item => item.Source == legFrom && item.Target == legTo)
                    ?? throw new InvalidOperationException(L($"Нет офлайн-модели для {legFrom} → {legTo}.",
                        $"No offline model is available for {legFrom} → {legTo}."));
                Add(leg);
            }
            if (!ArgosBridge.CanTranslate(known, from, to))
                throw new InvalidOperationException(L($"Нет офлайн-маршрута для {from} → {to}.",
                    $"No offline route is available for {from} → {to}."));
        }
        void Add(OfflineModelPackage item)
        {
            if (plan.Any(existing => existing.Source == item.Source && existing.Target == item.Target)) return;
            plan.Add(item);
            known.Add(new ArgosModel(item.Source, item.Target, ""));
        }
        AddDirection(source, target);
        if (bothDirections) AddDirection(target, source);
        return plan;
    }

    private static async Task<string> EnsureEngineAsync(IProgress<string> progress, CancellationToken cancel)
    {
        var venvPython = ArgosBridge.InstalledPython;
        if (File.Exists(venvPython))
        {
            try { await ArgosBridge.ListAsync(venvPython, cancel); return venvPython; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* Repair this app's incomplete environment below. */ }
        }
        Directory.CreateDirectory(Root);
        var python = File.Exists(PythonExecutable) ? PythonExecutable :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python313", "python.exe");
        if (!File.Exists(python))
        {
            var installer = Path.Combine(Root, "Downloads", "python-3.13.17-amd64.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(installer)!);
            try
            {
                progress.Report("Downloading Python from python.org…");
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                await DownloadAsync(client, new Uri(PythonUrl), installer, 80L * 1024 * 1024, cancel);
                await using var installerStream = File.OpenRead(installer);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(installerStream, cancel));
                if (!hash.Equals(PythonSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L("Контрольная сумма установщика Python не совпала с python.org.",
                        "Python installer checksum does not match python.org."));
                progress.Report("Installing Python for this user…");
                await RunProcessAsync(installer, ["/quiet", "InstallAllUsers=0",
                    "TargetDir=" + PythonDirectory, "Include_pip=1", "Include_launcher=0",
                    "PrependPath=0", "Include_test=0", "Include_tcltk=0",
                    "Shortcuts=0", "AssociateFiles=0"], TimeSpan.FromMinutes(20), cancel);
            }
            finally { if (File.Exists(installer)) File.Delete(installer); }
            python = PythonExecutable;
            if (!File.Exists(python)) throw new InvalidOperationException(L(
                "Установка Python завершилась, но python.exe не найден.",
                "Python installation finished without python.exe."));
        }
        if (!File.Exists(venvPython))
        {
            progress.Report("Creating a private Python environment for Kliker Blocks…");
            await RunProcessAsync(python, ["-m", "venv", Path.GetDirectoryName(Path.GetDirectoryName(venvPython)!)!],
                TimeSpan.FromMinutes(5), cancel);
        }
        progress.Report("Installing the Argos translation engine from PyPI…");
        await RunProcessAsync(venvPython, ["-m", "pip", "install", "--disable-pip-version-check",
            "--only-binary=:all:", "argostranslate"], TimeSpan.FromMinutes(30), cancel);
        await ArgosBridge.ListAsync(venvPython, cancel);
        return venvPython;
    }

    private static async Task DownloadAsync(HttpClient client, Uri url, string path, long maximum,
        CancellationToken cancel)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long size && size > maximum)
            throw new InvalidDataException(L("Загрузка превышает допустимый размер.",
                "Download exceeds the allowed size."));
        await using var input = await response.Content.ReadAsStreamAsync(cancel);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[128 * 1024];
        long written = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancel)) > 0)
        {
            written += read;
            if (written > maximum) throw new InvalidDataException(L("Загрузка превышает допустимый размер.",
                "Download exceeds the allowed size."));
            await output.WriteAsync(buffer.AsMemory(0, read), cancel);
        }
    }

    private static async Task RunProcessAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancel)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var item in arguments) info.ArgumentList.Add(item);
        using var process = Process.Start(info) ?? throw new InvalidOperationException(L(
            "Установщик не запустился.", "The installer did not start."));
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(timeout);
        try
        {
            var errorTask = process.StandardError.ReadToEndAsync(limit.Token);
            var outputTask = process.StandardOutput.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token);
            var error = await errorTask;
            var output = await outputTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(L($"Установщик завершился с кодом {process.ExitCode}: ",
                    $"Installer exited with code {process.ExitCode}: ") +
                    (error.Length > 0 ? error : output)[..Math.Min(600, (error.Length > 0 ? error : output).Length)]);
        }
        finally
        {
            if (!process.HasExited) try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
        }
    }
}
