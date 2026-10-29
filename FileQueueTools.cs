using System.Text.Json;

namespace KlikerBlocks;

internal sealed class FileQueueRecord
{
    public string Status { get; set; } = "";
    public DateTimeOffset LeaseUntilUtc { get; set; }
}

internal static class FileQueueTools
{
    internal static (string File, string Outcome) Execute(string folder, string mask, string statePath,
        string action, string currentFile, DateTimeOffset now)
    {
        folder = Path.GetFullPath(folder);
        statePath = Path.GetFullPath(statePath);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Папка очереди не найдена: " + folder);
        if (string.IsNullOrWhiteSpace(mask) || mask.IndexOfAny(['/', '\\']) >= 0)
            throw new InvalidOperationException("Маска очереди не должна содержать путь.");
        var stateDir = Path.GetDirectoryName(statePath)!;
        Directory.CreateDirectory(stateDir);
        using var gate = AcquireLock(statePath + ".lock");
        var states = File.Exists(statePath)
            ? JsonSerializer.Deserialize<Dictionary<string, FileQueueRecord>>(File.ReadAllText(statePath)) ?? []
            : new Dictionary<string, FileQueueRecord>(StringComparer.OrdinalIgnoreCase);
        string result;
        string outcome;
        switch (action)
        {
            case "Взять следующий":
            {
                var files = Directory.EnumerateFiles(folder, mask, SearchOption.TopDirectoryOnly)
                    .Where(path => !Path.GetFullPath(path).StartsWith(statePath,
                        StringComparison.OrdinalIgnoreCase))
                    .Take(10001).ToArray();
                if (files.Length > 10000) throw new InvalidOperationException("В очереди более 10000 файлов.");
                var next = files.Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault(path =>
                    !states.TryGetValue(Path.GetFullPath(path), out var record) ||
                    record.Status == "leased" && record.LeaseUntilUtc <= now);
                if (next is null) { result = ""; outcome = "empty"; break; }
                result = Path.GetFullPath(next);
                states[result] = new FileQueueRecord { Status = "leased", LeaseUntilUtc = now.AddMinutes(15) };
                outcome = "picked";
                break;
            }
            case "Отметить готовым":
            case "Отметить ошибкой":
            {
                if (string.IsNullOrWhiteSpace(currentFile))
                    throw new InvalidOperationException("Укажите текущий файл очереди.");
                result = Path.GetFullPath(currentFile);
                if (!result.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Файл не принадлежит папке очереди.");
                states[result] = new FileQueueRecord { Status = action == "Отметить готовым" ? "done" : "failed" };
                outcome = "marked";
                break;
            }
            default: throw new InvalidOperationException("Неизвестная операция очереди файлов.");
        }
        var temporary = statePath + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(states));
            File.Move(temporary, statePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return (result, outcome);
    }

    private static FileStream AcquireLock(string path)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 39) { Thread.Sleep(50); }
        }
        throw new IOException("Очередь файлов занята другим процессом.");
    }
}
