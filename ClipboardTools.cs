namespace KlikerBlocks;

internal static class ClipboardTools
{
    public static Task SetTextAsync(string text, CancellationToken cancel)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                Clipboard.SetText(text);
                completion.SetResult();
            }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    public static Task<string> GetTextAsync(CancellationToken cancel)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                completion.SetResult(Clipboard.ContainsText() ? Clipboard.GetText() : "");
            }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }
}
