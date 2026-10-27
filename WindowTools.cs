using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace KlikerBlocks;

internal sealed record WindowCandidate(IntPtr Handle, string Title, string ProcessName, int ProcessId)
{
    public override string ToString() => $"{Title} — {ProcessName}.exe (HWND 0x{Handle.ToInt64():X}, PID {ProcessId})";
}

internal static class WindowTools
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int count);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr hwnd);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attached);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutText(IntPtr hwnd, uint message, IntPtr wParam,
        StringBuilder text, uint flags, uint milliseconds, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutSetText(IntPtr hwnd, uint message, IntPtr wParam,
        string text, uint flags, uint milliseconds, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutRaw(IntPtr hwnd, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint milliseconds, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativePostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y) { public readonly int X = x, Y = y; }

    public static IReadOnlyList<WindowCandidate> ListOpenWindows()
    {
        var windows = new List<WindowCandidate>();
        EnumWindows((hwnd, _) =>
        {
            if (IsWindowVisible(hwnd) && Describe(hwnd) is { Title.Length: > 0 } candidate)
                windows.Add(candidate);
            return true;
        }, IntPtr.Zero);
        return windows.OrderBy(window => window.ProcessName).ThenBy(window => window.Title).ToArray();
    }

    private static WindowCandidate? Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return null;
        var length = GetWindowTextLength(hwnd);
        var text = new StringBuilder(Math.Max(1, length + 1));
        if (length > 0) GetWindowText(hwnd, text, text.Capacity);
        GetWindowThreadProcessId(hwnd, out var processId);
        string processName;
        try { processName = Process.GetProcessById((int)processId).ProcessName; }
        catch { processName = ""; }
        return new WindowCandidate(hwnd, text.ToString(), processName, (int)processId);
    }

    private static string NormalizeProcess(string name) =>
        name.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name.Trim()[..^4] : name.Trim();

    private static bool Matches(WindowCandidate candidate, WindowScopeSettings settings) => settings.MatchMode switch
    {
        "Процесс" => candidate.ProcessName.Equals(NormalizeProcess(settings.ProcessName), StringComparison.OrdinalIgnoreCase),
        "Процесс и заголовок" => candidate.ProcessName.Equals(NormalizeProcess(settings.ProcessName), StringComparison.OrdinalIgnoreCase) &&
            candidate.Title.Contains(settings.TitleContains, StringComparison.OrdinalIgnoreCase),
        "Заголовок" => candidate.Title.Contains(settings.TitleContains, StringComparison.OrdinalIgnoreCase),
        "HWND" => candidate.Handle.ToInt64() == settings.WindowHandle &&
            (settings.ProcessId == 0 || candidate.ProcessId == settings.ProcessId),
        _ => false
    };

    public static IntPtr FindTarget(WindowScopeSettings settings)
    {
        if (!settings.Enabled) return IntPtr.Zero;
        if (settings.MatchMode == "HWND")
        {
            try
            {
                var handle = new IntPtr(settings.WindowHandle);
                var candidate = Describe(handle);
                return candidate is not null && IsWindowVisible(handle) && Matches(candidate, settings) ? handle : IntPtr.Zero;
            }
            catch (OverflowException) { return IntPtr.Zero; }
        }
        return ListOpenWindows().FirstOrDefault(window => Matches(window, settings))?.Handle ?? IntPtr.Zero;
    }

    public static bool IsTargetForeground(WindowScopeSettings settings)
    {
        if (!settings.Enabled) return true;
        var handle = GetForegroundWindow();
        return Describe(handle) is { } candidate && Matches(candidate, settings);
    }

    public static WindowCandidate? GetWindow(IntPtr handle) => Describe(handle);

    public static IntPtr Find(string title, bool prefixOnly = false)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Укажите часть заголовка окна.");
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var length = GetWindowTextLength(hwnd);
            if (length <= 0) return true;
            var text = new StringBuilder(length + 1);
            GetWindowText(hwnd, text, text.Capacity);
            var caption = text.ToString();
            if (prefixOnly ? !caption.StartsWith(title, StringComparison.OrdinalIgnoreCase)
                : !caption.Contains(title, StringComparison.OrdinalIgnoreCase)) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr ElementAt(Point screenPoint) => WindowFromPoint(new NativePoint(screenPoint.X, screenPoint.Y));

    public static MouseMappingContext MouseContext(Point point)
    {
        MouseMappingWindow Read(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return new MouseMappingWindow("", "", "", 0, 0);
            var root = GetAncestor(handle, 2);
            if (root == IntPtr.Zero) root = handle;
            var candidate = Describe(root);
            if (candidate is null || !GetWindowRect(root, out var bounds))
                return new MouseMappingWindow("", "", "", 0, 0);
            var classText = new StringBuilder(256);
            GetClassName(handle, classText, classText.Capacity);
            return new MouseMappingWindow(candidate.ProcessName, candidate.Title, classText.ToString(),
                point.X - bounds.Left, point.Y - bounds.Top);
        }
        return new MouseMappingContext(Read(GetForegroundWindow()), Read(ElementAt(point)));
    }

    public static int ProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return 0;
        GetWindowThreadProcessId(hwnd, out var processId);
        return checked((int)processId);
    }

    public static string ElementText(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return "";
        // WM_GETTEXT works with standard controls in other processes; cap text and response time.
        var text = new StringBuilder(4097);
        const uint wmGetText = 0x000D;
        const uint abortIfHung = 0x0002;
        if (SendMessageTimeoutText(hwnd, wmGetText, new IntPtr(text.Capacity), text,
            abortIfHung, 500, out _) != IntPtr.Zero)
            return text.ToString();
        var length = Math.Min(GetWindowTextLength(hwnd), 4096);
        var fallback = new StringBuilder(length + 1);
        if (length > 0) GetWindowText(hwnd, fallback, fallback.Capacity);
        return fallback.ToString();
    }

    public static void SetElementText(IntPtr hwnd, string text)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) throw new InvalidOperationException("Элемент с указанным HWND не найден.");
        if (text.Length > 4096) throw new InvalidOperationException("Текст элемента не должен превышать 4096 символов.");
        const uint wmSetText = 0x000C;
        const uint abortIfHung = 0x0002;
        if (SendMessageTimeoutSetText(hwnd, wmSetText, IntPtr.Zero, text,
            abortIfHung, 500, out _) == IntPtr.Zero)
            throw new InvalidOperationException("Windows не разрешила изменить текст элемента или он не ответил за 500 мс.");
    }

    public static long SendNumericMessage(IntPtr hwnd, int message, long wParam, long lParam, bool post)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) throw new InvalidOperationException("Окно с указанным HWND не найдено.");
        if (message is < 0 or > 65535) throw new InvalidOperationException("Код сообщения должен быть 0–65535.");
        var w = new IntPtr(wParam);
        var l = new IntPtr(lParam);
        if (post)
        {
            if (!NativePostMessage(hwnd, (uint)message, w, l))
                throw new InvalidOperationException("Windows не разрешила отправить сообщение POSTMESSAGE.");
            return 1;
        }
        const uint abortIfHung = 0x0002;
        if (SendMessageTimeoutRaw(hwnd, (uint)message, w, l, abortIfHung, 500, out var result) == IntPtr.Zero)
            throw new InvalidOperationException("Окно не ответило на SENDMESSAGE за 500 мс или Windows запретила сообщение.");
        return result.ToInt64();
    }

    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Окно не найдено.");
        ShowWindow(hwnd, 9); // SW_RESTORE
        if (GetForegroundWindow() == hwnd) return;
        _ = SetForegroundWindow(hwnd);
        if (GetForegroundWindow() == hwnd) return;
        // A foreground application (for example Notepad) can own a different
        // input queue. Temporarily joining that queue permits normal focus
        // transfer without sending a fake keystroke to the user's desktop.
        var currentThread = GetCurrentThreadId();
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var targetThread = GetWindowThreadProcessId(hwnd, out _);
        var joinedForeground = foregroundThread != 0 && foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, true);
        var joinedTarget = targetThread != 0 && targetThread != currentThread &&
            targetThread != foregroundThread && AttachThreadInput(currentThread, targetThread, true);
        try
        {
            _ = BringWindowToTop(hwnd);
            _ = SetForegroundWindow(hwnd);
            _ = SetActiveWindow(hwnd);
        }
        finally
        {
            if (joinedTarget) _ = AttachThreadInput(currentThread, targetThread, false);
            if (joinedForeground) _ = AttachThreadInput(currentThread, foregroundThread, false);
        }
        if (GetForegroundWindow() != hwnd)
            throw new InvalidOperationException("Windows не разрешила сделать окно активным. Проверьте, что Kliker Blocks и целевая программа запущены с одинаковыми правами.");
    }

    public static bool IsForegroundHandle(IntPtr hwnd) => hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;

    public static void Move(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Окно не найдено.");
        if (width < 1 || height < 1) throw new ArgumentException("Ширина и высота окна должны быть положительными.");
        if (!MoveWindow(hwnd, x, y, width, height, true))
            throw new InvalidOperationException("Windows не разрешила изменить размер или положение окна.");
    }

    public static void RaiseWithoutActivating(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            throw new InvalidOperationException("Окно с указанным HWND не найдено.");
        const uint noMoveNoSizeNoActivate = 0x0001 | 0x0002 | 0x0010;
        if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, noMoveNoSizeNoActivate))
            throw new InvalidOperationException("Windows не разрешила показать окно поверх остальных без смены фокуса.");
    }

    public static Rectangle Bounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !GetWindowRect(hwnd, out var rectangle))
            throw new InvalidOperationException("Окно с указанным HWND не найдено или его геометрия недоступна.");
        return Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
    }

    public static void Position(IntPtr hwnd, int x, int y)
    {
        var bounds = Bounds(hwnd);
        Move(hwnd, x, y, bounds.Width, bounds.Height);
    }

    public static void Size(IntPtr hwnd, int width, int height)
    {
        var bounds = Bounds(hwnd);
        Move(hwnd, bounds.Left, bounds.Top, width, height);
    }

    public static void State(IntPtr hwnd, bool restored)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) throw new InvalidOperationException("Окно с указанным HWND не найдено.");
        ShowWindow(hwnd, restored ? 9 : 6); // SW_RESTORE / SW_MINIMIZE
    }

}
