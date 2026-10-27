using System.Diagnostics;
using System.Security.Principal;

namespace KlikerBlocks;

// A named mutex is shared by all KLIKER processes in the same Windows session.
// Its owning thread stays alive for the entire lease, including asynchronous blocks.
internal static class DesktopInputCoordinator
{
    private static readonly string MutexName = "Local\\KlikerBlocks.DesktopInput." +
        (WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName);

    public static async Task<DesktopInputLease> AcquireAsync(CancellationToken cancel, Action<string>? log = null)
    {
        cancel.ThrowIfCancellationRequested();
        var ready = new TaskCompletionSource<DesktopInputLease>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Factory.StartNew(() =>
        {
            Mutex? mutex = null;
            var acquired = false;
            var waitingReported = false;
            var timer = Stopwatch.StartNew();
            try
            {
                mutex = new Mutex(false, MutexName);
                while (!acquired)
                {
                    cancel.ThrowIfCancellationRequested();
                    try { acquired = mutex.WaitOne(50); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired && !waitingReported && timer.ElapsedMilliseconds >= 150)
                    {
                        waitingReported = true;
                        SafeLog(log, "Ожидание очереди мыши и клавиатуры…");
                    }
                }
                var lease = new DesktopInputLease();
                if (ready.TrySetResult(lease))
                {
                    if (waitingReported) SafeLog(log, "Очередь ввода получена.");
                    lease.WaitForRelease();
                }
            }
            catch (Exception error) { ready.TrySetException(error); }
            finally
            {
                if (acquired) mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        using var registration = cancel.Register(() => ready.TrySetCanceled(cancel));
        return await ready.Task.ConfigureAwait(false);
    }

    private static void SafeLog(Action<string>? log, string message)
    {
        try { log?.Invoke(message); }
        catch { /* Logging must not release an active desktop lease. */ }
    }
}

internal sealed class DesktopInputLease : IDisposable
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _released.TrySetResult();
    }

    internal void WaitForRelease() => _released.Task.GetAwaiter().GetResult();
}
