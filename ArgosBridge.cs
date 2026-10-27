using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed record ArgosModel(string Source, string Target, string Name);
internal sealed record ArgosPackageInfo(string Source, string Target, string SourceName, string TargetName);

internal static class ArgosBridge
{
    private const int MaxOutput = 1024 * 1024;
    private const string ListScript = """
        import json
        import os
        from pathlib import Path
        import argostranslate.settings as settings
        import argostranslate.package as package
        bundled = os.environ.get('KLIKER_BUNDLED_MODELS')
        if bundled and Path(bundled).is_dir():
            settings.package_dirs.append(Path(bundled))
        print(json.dumps([{'source': p.from_code, 'target': p.to_code, 'name': str(p)} for p in package.get_installed_packages()], ensure_ascii=False))
        """;
    private const string InstallScript = """
        import sys
        from pathlib import Path
        import argostranslate.package as package
        package.install_from_path(Path(sys.argv[1]))
        print('OK')
        """;
    private const string TranslateScript = """
        import json
        import os
        import re
        import sys
        from pathlib import Path
        import argostranslate.settings as settings
        import argostranslate.translate as translation
        class OfflineSentencizer:
            def __init__(self, pkg): pass
            def split_sentences(self, text):
                return [part for part in re.split(r'(?<=[.!?。！？])\s+|\n+', text) if part] or [text]
        translation.StanzaSentencizer = OfflineSentencizer
        translation.MiniSBDSentencizer = OfflineSentencizer
        bundled = os.environ.get('KLIKER_BUNDLED_MODELS')
        if bundled and Path(bundled).is_dir():
            settings.package_dirs.append(Path(bundled))
        source, target = sys.argv[1:3]
        languages = {language.code: language for language in translation.get_installed_languages()}
        if source not in languages or target not in languages:
            raise RuntimeError('Language pair is not installed')
        result = languages[source].get_translation(languages[target]).translate(sys.stdin.read())
        print(json.dumps({'result': result}, ensure_ascii=False))
        """;

    public static string ModelsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KLIKER Blocks", "LanguageModels", "Packages");
    private static string RuntimeRoot => Environment.GetEnvironmentVariable("KLIKER_TRANSLATION_RUNTIME") is { Length: > 0 } testPath
        ? testPath : Path.Combine(AppContext.BaseDirectory, "TranslationRuntime");
    public static string BundledModelsDirectory => Path.Combine(RuntimeRoot, "models");
    public static string BundledPython => Path.Combine(RuntimeRoot, "python", "python.exe");
    public static string InstalledPython => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KLIKER Blocks", "TranslationRuntime", "venv", "Scripts", "python.exe");

    public static async Task<IReadOnlyList<ArgosModel>> ListAsync(string pythonPath, CancellationToken cancel)
    {
        var json = await RunAsync(pythonPath, ListScript, [], null, TimeSpan.FromSeconds(60), cancel);
        try
        {
            var items = JsonSerializer.Deserialize<List<ArgosModelJson>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return items.Where(item => item.Source is not null && item.Target is not null)
                .Select(item => new ArgosModel(item.Source!, item.Target!, item.Name ?? ""))
                .OrderBy(item => item.Source).ThenBy(item => item.Target).ToArray();
        }
        catch (JsonException error) { throw new InvalidOperationException("Argos вернул неверный список моделей.", error); }
    }

    public static bool CanTranslate(IReadOnlyList<ArgosModel> models, string source, string target)
    {
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) return true;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source };
        var queue = new Queue<string>();
        queue.Enqueue(source);
        while (queue.TryDequeue(out var current))
            foreach (var next in models.Where(model => model.Source.Equals(current,
                         StringComparison.OrdinalIgnoreCase)).Select(model => model.Target))
                if (seen.Add(next))
                {
                    if (next.Equals(target, StringComparison.OrdinalIgnoreCase)) return true;
                    queue.Enqueue(next);
                }
        return false;
    }

    public static async Task InstallFileAsync(string pythonPath, string packagePath, CancellationToken cancel)
    {
        var file = Path.GetFullPath(packagePath);
        ReadPackageInfo(file);
        await RunAsync(pythonPath, InstallScript, [file], null, TimeSpan.FromMinutes(5), cancel);
    }

    public static ArgosPackageInfo ReadPackageInfo(string packagePath)
    {
        var file = Path.GetFullPath(packagePath);
        if (!file.EndsWith(".argosmodel", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
            throw new InvalidOperationException("Выберите существующий файл .argosmodel.");
        if (new FileInfo(file).Length > 1024L * 1024 * 1024)
            throw new InvalidOperationException("Пакет больше 1 ГиБ; установка отменена.");
        try
        {
            using var archive = ZipFile.OpenRead(file);
            if (archive.Entries.Count > 10000)
                throw new InvalidOperationException("В пакете слишком много файлов.");
            long expandedSize = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith('/') || name.Contains(':') ||
                    name.Split('/').Any(part => part is "." or ".."))
                    throw new InvalidOperationException("Пакет содержит небезопасные пути.");
                expandedSize += entry.Length;
                if (expandedSize > 4L * 1024 * 1024 * 1024)
                    throw new InvalidOperationException("Распакованный пакет слишком большой.");
            }
            var metadata = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.EndsWith("/metadata.json", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("metadata.json", StringComparison.OrdinalIgnoreCase));
            if (metadata is null || metadata.Length > 32 * 1024)
                throw new InvalidOperationException("В пакете нет корректного metadata.json.");
            using var stream = metadata.Open();
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var source = root.GetProperty("from_code").GetString() ?? "";
            var target = root.GetProperty("to_code").GetString() ?? "";
            if (source.Length is < 2 or > 8 || target.Length is < 2 or > 8 ||
                !source.All(char.IsLetter) || !target.All(char.IsLetter))
                throw new InvalidOperationException("В пакете указаны неверные коды языков.");
            return new ArgosPackageInfo(source.ToLowerInvariant(), target.ToLowerInvariant(),
                root.TryGetProperty("from_name", out var fromName) ? fromName.GetString() ?? source : source,
                root.TryGetProperty("to_name", out var toName) ? toName.GetString() ?? target : target);
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or KeyNotFoundException)
        { throw new InvalidOperationException("Файл .argosmodel повреждён или имеет неизвестный формат.", error); }
    }

    public static async Task<string> TranslateAsync(string text, string source, string target,
        string pythonPath, int timeoutMs, CancellationToken cancel)
    {
        if (!TranslationTools.IsLanguage(source) || !TranslationTools.IsLanguage(target))
            throw new InvalidOperationException("Локальный перевод: укажите конкретные языки ru/en/fr/zh/es/de (не auto).");
        if (text.Length > 16000) throw new InvalidOperationException("Перевод: текст длиннее 16000 символов.");
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) return text;
        var json = await RunAsync(pythonPath, TranslateScript,
            [source.ToLowerInvariant(), target.ToLowerInvariant()], text,
            TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 100, 60000)), cancel);
        try
        {
            using var result = JsonDocument.Parse(json);
            return result.RootElement.GetProperty("result").GetString() ?? "";
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new InvalidOperationException("Argos вернул неверный результат перевода.", error); }
    }

    private static async Task<string> RunAsync(string pythonPath, string script, IReadOnlyList<string> args,
        string? input, TimeSpan timeout, CancellationToken cancel)
    {
        var explicitPath = !string.IsNullOrWhiteSpace(pythonPath) &&
            !pythonPath.Trim().Equals("python", StringComparison.OrdinalIgnoreCase) &&
            !pythonPath.Trim().Equals("Встроенный", StringComparison.OrdinalIgnoreCase);
        var selectedPython = explicitPath ? pythonPath.Trim() : File.Exists(InstalledPython)
            ? InstalledPython : File.Exists(BundledPython) ? BundledPython : "";
        if (string.IsNullOrWhiteSpace(selectedPython) || !File.Exists(selectedPython))
            throw new InvalidOperationException(UiLanguage.T(
                "Локальный перевод не установлен. Откройте «Языки → Скачать офлайн-перевод…» и выберите пару языков."));
        Directory.CreateDirectory(ModelsDirectory);
        var start = new ProcessStartInfo(selectedPython)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (input is not null) start.StandardInputEncoding = Encoding.UTF8;
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["ARGOS_PACKAGES_DIR"] = ModelsDirectory;
        start.Environment["KLIKER_BUNDLED_MODELS"] = BundledModelsDirectory;
        // Argos scans every direct child of its package directory as a model.
        // Cache/config must stay outside Packages or loading fails on missing metadata.json.
        var argosData = Path.Combine(Path.GetDirectoryName(ModelsDirectory)!, "RuntimeData");
        Directory.CreateDirectory(argosData);
        start.Environment["XDG_DATA_HOME"] = argosData;
        start.Environment["XDG_CONFIG_HOME"] = argosData;
        start.Environment["XDG_CACHE_HOME"] = argosData;
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { throw new InvalidOperationException("Не удалось запустить Python. Проверьте путь к интерпретатору.", error); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(timeout);
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var errorTask = process.StandardError.ReadToEndAsync(deadline.Token);
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), deadline.Token);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(deadline.Token);
            var output = await outputTask;
            var error = await errorTask;
            if (output.Length > MaxOutput || error.Length > MaxOutput)
                throw new InvalidOperationException("Вывод Argos слишком большой.");
            if (process.ExitCode != 0)
                throw new InvalidOperationException(error.Contains("No module named 'argostranslate'", StringComparison.OrdinalIgnoreCase)
                    ? "Не установлен argostranslate в выбранном Python."
                    : "Ошибка Argos: " + error.Trim()[..Math.Min(error.Trim().Length, 700)]);
            return output.Trim();
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        { throw new TimeoutException("Argos не ответил за заданное время."); }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    private sealed class ArgosModelJson
    {
        public string? Source { get; set; }
        public string? Target { get; set; }
        public string? Name { get; set; }
    }
}
