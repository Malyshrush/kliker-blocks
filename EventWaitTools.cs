namespace KlikerBlocks;

internal static class EventWaitTools
{
    internal static async Task<bool> WindowAsync(string title, bool appear, int timeoutMs,
        CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Укажите часть заголовка окна для ожидания.");
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            cancel.ThrowIfCancellationRequested();
            if ((WindowTools.Find(title) != IntPtr.Zero) == appear) return true;
            var remaining = deadline - Environment.TickCount64;
            if (remaining <= 0) return false;
            await Task.Delay((int)Math.Min(200, remaining), cancel).ConfigureAwait(false);
        } while (true);
    }

    internal static async Task<bool> FileAsync(string path, int timeoutMs, CancellationToken cancel)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(directory, Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false
        };
        FileSystemEventHandler changed = (_, _) => completion.TrySetResult(true);
        RenamedEventHandler renamed = (_, _) => completion.TrySetResult(true);
        ErrorEventHandler failed = (_, error) => completion.TrySetException(error.GetException());
        watcher.Changed += changed;
        watcher.Created += changed;
        watcher.Deleted += changed;
        watcher.Renamed += renamed;
        watcher.Error += failed;
        watcher.EnableRaisingEvents = true;
        try
        {
            var winner = await Task.WhenAny(completion.Task, Task.Delay(timeoutMs, cancel)).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();
            return winner == completion.Task && await completion.Task.ConfigureAwait(false);
        }
        finally { watcher.EnableRaisingEvents = false; }
    }
}
