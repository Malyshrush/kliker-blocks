namespace KlikerBlocks;

// Each MainForm owns one inbox; two KLIKER processes never share received messages.
internal sealed class WindowsMessageInbox
{
    private readonly object _gate = new();
    private long _wParam;
    private long _lParam;
    private long _version;

    public void Record(long wParam, long lParam)
    {
        lock (_gate)
        {
            _wParam = wParam;
            _lParam = lParam;
            _version++;
        }
    }

    public (long WParam, long LParam, bool IsNew, long Version) Read(long seenVersion)
    {
        lock (_gate) return (_wParam, _lParam, _version != seenVersion, _version);
    }
}
