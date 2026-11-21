using System.Runtime.InteropServices;

namespace KlikerBlocks;

// Limit pending UI work while preserving the latest entries. File logging is independent.
internal sealed class PendingJournalEntries(int capacity = 1000)
{
    private readonly object _sync = new();
    private readonly Queue<string> _items = new();
    private int _dropped;

    internal void Enqueue(string entry)
    {
        lock (_sync)
        {
            if (_items.Count >= capacity) { _items.Dequeue(); _dropped++; }
            _items.Enqueue(entry);
        }
    }

    internal (int Dropped, string[] Entries) Take(int limit)
    {
        lock (_sync)
        {
            var dropped = _dropped;
            _dropped = 0;
            var entries = new List<string>(Math.Min(limit, _items.Count));
            while (entries.Count < limit && _items.TryDequeue(out var entry)) entries.Add(entry);
            return (dropped, entries.ToArray());
        }
    }

    internal void Clear()
    {
        lock (_sync) { _items.Clear(); _dropped = 0; }
    }
}

internal sealed class ExecutionJournal : RichTextBox
{
    private const int MaxEntries = 500;
    private readonly Queue<(string Text, int Length)> _entries = new();

    internal ExecutionJournal()
    {
        ReadOnly = true;
        DetectUrls = false;
        HideSelection = false;
        BackColor = SystemColors.Window;
        ScrollBars = RichTextBoxScrollBars.Both;
        MaxLength = int.MaxValue;
        var menu = new ContextMenuStrip();
        var copy = menu.Items.Add(UiLanguage.Code == "ru" ? "Копировать" : "Copy", null, (_, _) => Copy());
        var select = menu.Items.Add(UiLanguage.Code == "ru" ? "Выделить всё" : "Select all", null, (_, _) => SelectAll());
        menu.Opening += (_, _) => { copy.Enabled = SelectionLength > 0; select.Enabled = TextLength > 0; };
        ContextMenuStrip = menu;
    }

    internal string[] Snapshot() => _entries.Select(entry => entry.Text).ToArray();

    internal void ClearEntries()
    {
        _entries.Clear();
        Clear();
    }

    internal void AddEntry(string text)
    {
        var selectionStart = SelectionStart;
        var selectionEnd = selectionStart + SelectionLength;
        var scroll = new Point();
        var hasHandle = IsHandleCreated;
        if (hasHandle) SendMessage(Handle, 0x04DD, IntPtr.Zero, ref scroll); // EM_GETSCROLLPOS
        var follow = SelectionLength == 0 && (!hasHandle || AtBottom());
        var removed = 0;
        if (_entries.Count == MaxEntries)
        {
            removed = _entries.Peek().Length;
            // RichEdit ignores an empty replacement while the control is read-only.
            ReadOnly = false;
            try { Select(0, removed); SelectedText = ""; }
            finally { ReadOnly = true; }
            _entries.Dequeue();
        }
        var previousLength = TextLength;
        AppendText(text + "\n");
        _entries.Enqueue((text, TextLength - previousLength));
        if (follow)
        {
            Select(TextLength, 0);
            ScrollToCaret();
        }
        else
        {
            var start = Math.Clamp(selectionStart - removed, 0, TextLength);
            var end = Math.Clamp(selectionEnd - removed, start, TextLength);
            Select(start, end - start);
            if (hasHandle) SendMessage(Handle, 0x04DE, IntPtr.Zero, ref scroll); // EM_SETSCROLLPOS
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.A)) { SelectAll(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool AtBottom()
    {
        var info = new ScrollInfo { Size = (uint)Marshal.SizeOf<ScrollInfo>(), Mask = 7 };
        return !GetScrollInfo(Handle, 1, ref info) || info.Position >= info.Maximum - Math.Max(0, (long)info.Page - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ContextMenuStrip?.Dispose();
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        public uint Size, Mask;
        public int Minimum, Maximum;
        public uint Page;
        public int Position, TrackPosition;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, ref Point lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetScrollInfo(IntPtr hwnd, int bar, ref ScrollInfo info);
}
