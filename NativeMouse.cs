using System.Drawing;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class NativeMouse
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, [In] NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);

    public static void EnableDpiAwareness()
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { /* Windows may set DPI mode via manifest. */ }
    }

    public static Point Position()
    {
        if (!GetCursorPos(out var p)) throw new InvalidOperationException("Не удалось прочитать координаты курсора.");
        return new Point(p.X, p.Y);
    }

    public static void Move(Point point)
    {
        if (!SetCursorPos(point.X, point.Y)) throw new InvalidOperationException("Не удалось переместить курсор.");
    }

    public static void MoveInjected(Point point)
    {
        var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
        var x = (int)Math.Round((point.X - desktop.Left) * 65535.0 / Math.Max(1, desktop.Width - 1));
        var y = (int)Math.Round((point.Y - desktop.Top) * 65535.0 / Math.Max(1, desktop.Height - 1));
        SendMouseInput(0x0001u | 0x8000u | 0x4000u, x, y);
    }

    public static void MouseDownInjected() => SendMouseInput(0x0002u);
    public static void MouseUpInjected() => SendMouseInput(0x0004u);

    private static void SendMouseInput(uint flags, int x = 0, int y = 0)
    {
        var inputs = new[] { new NativeInput { Type = 0, Data = new InputUnion
            { Mouse = new MouseInput { X = x, Y = y, Flags = flags } } } };
        if (SendInput(1, inputs, Marshal.SizeOf<NativeInput>()) != 1)
            throw new InvalidOperationException("Windows не приняла ввод мыши в Paint.");
    }

    public static Point CenterFor(Point point)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = point.X, Y = point.Y }, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("Не удалось определить монитор курсора.");
        return new Point((info.Monitor.Left + info.Monitor.Right) / 2, (info.Monitor.Top + info.Monitor.Bottom) / 2);
    }

    public static void MouseDown(string button) => mouse_event(button switch
    {
        "Правая" or "ПКМ" => 0x0008u, "Средняя" or "СКМ" => 0x0020u,
        "Кнопка 4" => 0x0080u, "Кнопка 5" => 0x0080u, _ => 0x0002u
    }, 0, 0, button == "Кнопка 5" ? 2u : button == "Кнопка 4" ? 1u : 0u, UIntPtr.Zero);

    public static void MouseUp(string button) => mouse_event(button switch
    {
        "Правая" or "ПКМ" => 0x0010u, "Средняя" or "СКМ" => 0x0040u,
        "Кнопка 4" => 0x0100u, "Кнопка 5" => 0x0100u, _ => 0x0004u
    }, 0, 0, button == "Кнопка 5" ? 2u : button == "Кнопка 4" ? 1u : 0u, UIntPtr.Zero);

    public static void Wheel(int steps) => mouse_event(0x0800u, 0, 0, unchecked((uint)(steps * 120)), UIntPtr.Zero);

    [StructLayout(LayoutKind.Sequential)] private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    public static void TypeUnicode(string text)
    {
        foreach (var character in text)
        {
            var inputs = new[]
            {
                new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 0x0004 } } },
                new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 0x0004 | 0x0002 } } }
            };
            var inserted = SendInput(2, inputs, Marshal.SizeOf<NativeInput>());
            if (inserted != 2)
                throw new InvalidOperationException(UiLanguage.Format(
                    "Windows отклонила Unicode-ввод (SendInput={0}/2, Win32={1}).",
                    inserted, Marshal.GetLastWin32Error()));
        }
    }

    public static void MoveRelative(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0) return;
        var inputs = new[]
        {
            new NativeInput { Type = 0, Data = new InputUnion
                { Mouse = new MouseInput { X = deltaX, Y = deltaY, Flags = 0x0001 } } }
        };
        if (SendInput(1, inputs, Marshal.SizeOf<NativeInput>()) != 1)
            throw new InvalidOperationException("Windows не приняла относительное движение мыши. " +
                "Проверьте права приложения и журнал Windows; целевая программа также может блокировать программный ввод.");
    }

    public static void KeyDown(byte vk) => keybd_event(vk, 0, 0, UIntPtr.Zero);
    public static void KeyUp(byte vk) => keybd_event(vk, 0, 0x0002, UIntPtr.Zero);
    public static bool IsKeyDown(byte vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    public static (bool Down, bool Pressed) ReadKeyState(byte vk)
    {
        var state = GetAsyncKeyState(vk);
        return ((state & 0x8000) != 0, (state & 0x0001) != 0);
    }
    public static byte ParseControlKey(string name) => name.Trim().ToUpperInvariant() switch
    {
        "LMB" or "LBUTTON" or "ЛКМ" => 1,
        "RMB" or "RBUTTON" or "ПКМ" => 2,
        "MMB" or "MBUTTON" or "СКМ" => 4,
        "1" => 1, "2" => 2, "4" => 4, "5" => 5, "6" => 6,
        _ => ParseKey(name)
    };
    public static IntPtr WindowAt(Point point) => WindowFromPoint(new NativePoint { X = point.X, Y = point.Y });

    public static byte ParseKey(string name)
    {
        name = name.Trim().ToUpperInvariant();
        if (name.Length == 2 && name[0] == 'D' && name[1] is >= '0' and <= '9') return (byte)name[1];
        if (name.Length == 1 && (name[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')) return (byte)name[0];
        if (byte.TryParse(name, out var numeric) && numeric > 0) return numeric;
        if (name.StartsWith('F') && int.TryParse(name[1..], out var f) && f is >= 1 and <= 24) return (byte)(111 + f);
        return name switch
        {
            "ENTER" => 13, "SPACE" => 32, "TAB" => 9, "ESC" => 27,
            "CTRL" => 17, "ALT" => 18, "SHIFT" => 16, "BACKSPACE" => 8,
            "UP" => 38, "DOWN" => 40, "LEFT" => 37, "RIGHT" => 39,
            "HOME" => 36, "END" => 35, "DELETE" => 46, "INSERT" => 45,
            "PAGEUP" => 33, "PAGEDOWN" => 34, "WIN" => 91,
            _ => throw new ArgumentException($"Неизвестная клавиша: {name}")
        };
    }

    public static bool RegisterRunHotKey(IntPtr hwnd) => RegisterHotKey(hwnd, 1, 0x0003, 0x75); // Ctrl+Alt+F6
    public static bool RegisterStopHotKey(IntPtr hwnd) => RegisterHotKey(hwnd, 2, 0x0003, 0x76); // Ctrl+Alt+F7
    public static bool RegisterRecordingStopHotKey(IntPtr hwnd) => RegisterHotKey(hwnd, 5, 0x0003, 0x79); // Ctrl+Alt+F10
    public static bool RegisterUserHotKey(IntPtr hwnd, int id, uint modifiers, uint key) => RegisterHotKey(hwnd, id, modifiers, key);
    public static void ReleaseUserHotKey(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);
    public static void ReleaseHotKeys(IntPtr hwnd)
    {
        UnregisterHotKey(hwnd, 1);
        UnregisterHotKey(hwnd, 2);
        UnregisterHotKey(hwnd, 5);
    }
}

internal static class CursorMath
{
    public static Point Anchor(string mode, Point observed, Point monitorCenter, int x, int y) => mode switch
    {
        "Текущая точка" => observed,
        "Задать X/Y" => new Point(x, y),
        _ => monitorCenter
    };

    public static Point Target(string mode, Point observed, Point anchor, Point monitorCenter,
        int x, int y, int offsetX, int offsetY) => mode switch
    {
        "Задать X/Y" => new Point(x, y),
        "От смещённой точки" => new Point(observed.X + offsetX, observed.Y + offsetY),
        "От опоры" => new Point(anchor.X + offsetX, anchor.Y + offsetY),
        _ => monitorCenter
    };

    public static bool IsOutside(Point a, Point b, int threshold)
    {
        long dx = (long)a.X - b.X;
        long dy = (long)a.Y - b.Y;
        return dx * dx + dy * dy >= (long)threshold * threshold;
    }
}

internal sealed class StopHotkeyFallback : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int kind, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly HookProc _callback;
    private readonly Action _stopScenario;
    private readonly Action _stopRecording;
    private IntPtr _hook;

    public bool Available => _hook != IntPtr.Zero;

    public StopHotkeyFallback(Action stopScenario, Action stopRecording)
    {
        _stopScenario = stopScenario;
        _stopRecording = stopRecording;
        _callback = OnKey;
        _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
    }

    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() is 0x0100 or 0x0104 &&
            (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) != 0)
        {
            var key = Marshal.ReadInt32(data);
            if (key == 0x76) _stopScenario();
            if (key == 0x79) _stopRecording();
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
