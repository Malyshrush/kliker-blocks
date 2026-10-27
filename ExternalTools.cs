using System.Diagnostics;

namespace KlikerBlocks;

internal static class ExternalTools
{
    public static string ResolvePath(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Путь к файлу не указан.");
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(baseDirectory, value));
    }

    public static string FindAhkV2(string configured, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var explicitPath = ResolvePath(configured, baseDirectory);
            if (!File.Exists(explicitPath)) throw new FileNotFoundException("Интерпретатор AutoHotkey не найден.", explicitPath);
            return explicitPath;
        }
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) };
        foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x)))
            foreach (var name in new[] { "AutoHotkey64.exe", "AutoHotkey32.exe" })
            {
                var candidate = Path.Combine(root, "AutoHotkey", "v2", name);
                if (File.Exists(candidate)) return candidate;
            }
        throw new FileNotFoundException("AutoHotkey v2 не найден. Установите v2 или укажите путь к AutoHotkey64.exe в блоке.");
    }

    public static async Task<int?> StartAsync(string path, string arguments, bool wait, int timeoutMs,
        string baseDirectory, bool ahk, string interpreter, Action<string> log, CancellationToken cancel)
    {
        var fullPath = ResolvePath(path, baseDirectory);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Файл для запуска не найден.", fullPath);
        var executable = ahk ? FindAhkV2(interpreter, baseDirectory) : fullPath;
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(fullPath) ?? baseDirectory,
            UseShellExecute = !ahk,
            Arguments = ahk ? $"\"{fullPath}\" {arguments}".Trim() : arguments
        };
        using var process = Process.Start(info);
        if (process is null)
        {
            // Shell shortcuts and single-instance applications may launch successfully
            // without returning a process handle to the caller.
            log(UiLanguage.Format("Файл запущен через оболочку Windows: {0}", fullPath));
            if (wait) throw new InvalidOperationException(UiLanguage.T(
                "Windows запустила файл, но не вернула процесс для ожидания завершения. Отключите «Ждать завершения»."));
            return null;
        }
        log($"Запущено: {Path.GetFileName(fullPath)} (PID {process.Id})");
        if (!wait) return null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        if (timeoutMs > 0) deadline.CancelAfter(timeoutMs);
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new TimeoutException($"Ожидание процесса превысило {timeoutMs} мс. Сам процесс продолжает работу.");
        }
        log($"Процесс завершился с кодом {process.ExitCode}");
        return process.ExitCode;
    }
}
