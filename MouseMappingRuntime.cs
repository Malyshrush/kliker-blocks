using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KlikerBlocks;

internal sealed class MouseMappingRuntime : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct HookPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData
    { public HookPoint Point; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly Form _owner;
    private readonly MouseMappingSettings _settings;
    private readonly MouseMappingEngine _engine;
    private readonly HookProc _callback;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly CancellationTokenSource _actionCancel = new();
    private readonly Action _startScenario;
    private readonly Action _stopScenario;
    private readonly Action<string> _message;
    private readonly Action _stateChanged;
    private readonly Dictionary<Guid, byte[]> _heldKeys = [];
    private readonly Dictionary<Guid, string> _heldButtons = [];
    private readonly Dictionary<string, (int Layer, bool Suppress)> _temporaryLayers = [];
    private readonly HashSet<string> _suppressedDown = [];
    private readonly string _selfProcess = Process.GetCurrentProcess().ProcessName;
    private Mutex? _instanceGate;
    private bool _ownsGate;
    private IntPtr _hook;
    private bool _disposed;
    private string _lastStatus = "";
    private long _nextContextRefresh;

    public MouseMappingRuntime(Form owner, MouseMappingSettings settings, Action startScenario,
        Action stopScenario, Action<string> message, Action stateChanged)
    {
        _owner = owner;
        _settings = settings;
        _engine = new MouseMappingEngine(settings);
        _callback = OnMouse;
        _startScenario = startScenario;
        _stopScenario = stopScenario;
        _message = message;
        _stateChanged = stateChanged;
        _timer.Tick += (_, _) => Update();
    }

    public string ActiveDescription => _engine.ProfileIndex < 0 ? "профиль: нет" :
        $"{_engine.ProfileName} / {_engine.LayerName}";
    public IReadOnlyList<string> LayerNames => _engine.ProfileIndex < 0 ? [] :
        _settings.Profiles[_engine.ProfileIndex].Layers.Select(layer => layer.Name).ToArray();
    public void Start()
    {
        _instanceGate = new Mutex(false, @"Local\KLIKER.MouseMappings");
        try { _ownsGate = _instanceGate.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsGate = true; }
        if (!_ownsGate) throw new InvalidOperationException(UiLanguage.T(
            "Назначения мыши уже включены в другом экземпляре Kliker Blocks. Отключите их там перед включением здесь."));
        _hook = SetWindowsHookEx(14, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new InvalidOperationException("Windows не разрешила установить перехват мыши.");
        _timer.Start();
        Update();
    }

    public void SelectLayer(string name)
    {
        Dispatch(_engine.SwitchLayer(name));
        ReportStatus();
    }

    private void Update()
    {
        if (_disposed) return;
        try
        {
            var now = Environment.TickCount64;
            if (now >= _nextContextRefresh)
            {
                _nextContextRefresh = now + 120;
                RefreshContext(NativeMouse.Position());
            }
            Dispatch(_engine.Tick(now));
            ReportStatus();
        }
        catch (Exception error) { _message("Профиль мыши: " + error.Message); }
    }

    private void RefreshContext(Point point)
    {
        var context = WindowTools.MouseContext(point);
        var suspend = context.Foreground.ProcessName.Equals(_selfProcess, StringComparison.OrdinalIgnoreCase) ||
            context.Hovered.ProcessName.Equals(_selfProcess, StringComparison.OrdinalIgnoreCase);
        var nextProfile = suspend ? -1 : MouseProfileMatcher.Select(_settings, context);
        if (nextProfile != _engine.ProfileIndex && _temporaryLayers.Count > 0)
        {
            var oldLayer = _temporaryLayers.Values.First().Layer;
            Dispatch(_engine.SwitchLayer((oldLayer + 1).ToString()));
            _temporaryLayers.Clear();
        }
        Dispatch(suspend ? _engine.Suspend() : _engine.SetContext(context));
    }

    private void ReportStatus()
    {
        var current = ActiveDescription;
        if (current == _lastStatus) return;
        _lastStatus = current;
        _stateChanged();
    }

    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || _disposed) return CallNextHookEx(_hook, code, message, data);
        var raw = Marshal.PtrToStructure<MouseHookData>(data);
        if ((raw.Flags & 1) != 0) return CallNextHookEx(_hook, code, message, data);
        var parsed = ParseSource(message.ToInt32(), raw.MouseData);
        if (parsed is null) return CallNextHookEx(_hook, code, message, data);
        if (NativeMouse.IsKeyDown(17) && NativeMouse.IsKeyDown(18))
        {
            if (_temporaryLayers.Count > 0)
            {
                Dispatch(_engine.SwitchLayer((_temporaryLayers.Values.First().Layer + 1).ToString()));
                _temporaryLayers.Clear();
            }
            Dispatch(_engine.ReleaseActive());
            _suppressedDown.Clear();
            return CallNextHookEx(_hook, code, message, data); // Ctrl+Alt: аварийный доступ к интерфейсу.
        }
        var (source, down) = parsed.Value;
        try
        {
            if (down) RefreshContext(new Point(raw.Point.X, raw.Point.Y));
            var forceSuppress = false;
            if (!down && _temporaryLayers.Remove(source, out var temporary))
            {
                Dispatch(_engine.SwitchLayer((temporary.Layer + 1).ToString()));
                forceSuppress = temporary.Suppress;
            }
            var result = _engine.Handle(source, down, Environment.TickCount64);
            Dispatch(result.Commands);
            ReportStatus();
            if (down && result.Suppress && !MouseMappingNames.IsWheel(source)) _suppressedDown.Add(source);
            if (!down && _suppressedDown.Remove(source)) forceSuppress = true;
            return result.Suppress || forceSuppress ? new IntPtr(1) : CallNextHookEx(_hook, code, message, data);
        }
        catch (Exception error)
        {
            _message("Назначение мыши: " + error.Message);
            return CallNextHookEx(_hook, code, message, data);
        }
    }

    internal static (string Source, bool Down)? ParseSource(int message, uint data) => message switch
    {
        0x0201 => ("ЛКМ", true), 0x0202 => ("ЛКМ", false),
        0x0204 => ("ПКМ", true), 0x0205 => ("ПКМ", false),
        0x0207 => ("СКМ", true), 0x0208 => ("СКМ", false),
        0x020B => ((data >> 16) == 2 ? "Кнопка 5" : "Кнопка 4", true),
        0x020C => ((data >> 16) == 2 ? "Кнопка 5" : "Кнопка 4", false),
        0x020A => ((short)(data >> 16) > 0 ? "Колесо вверх" : "Колесо вниз", true),
        0x020E => ((short)(data >> 16) > 0 ? "Наклон вправо" : "Наклон влево", true),
        _ => null
    };

    private void Dispatch(IReadOnlyList<MouseMappingCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command.Binding.Action == "Переключить слой" && command.Press)
            {
                Dispatch(_engine.SwitchLayer(command.Binding.Value));
                continue;
            }
            if (command.Binding.Action == "Слой пока удерживается")
            {
                if (command.Press && !_temporaryLayers.ContainsKey(command.Binding.Source))
                {
                    _temporaryLayers[command.Binding.Source] = (_engine.LayerIndex, command.Binding.BlockOriginal);
                    Dispatch(_engine.SwitchLayer(command.Binding.Value));
                }
                continue;
            }
            if (_owner.IsHandleCreated && !_owner.IsDisposed)
                _owner.BeginInvoke(() => _ = ExecuteAsync(command));
        }
    }

    private async Task ExecuteAsync(MouseMappingCommand command)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (_disposed) return;
            var binding = command.Binding;
            switch (binding.Action)
            {
                case "Клавиши":
                    if (binding.Trigger is "Удержание" or "Переключатель")
                    {
                        if (command.Press)
                        {
                            var keys = MouseKeySequence.ParseChord(binding.Value);
                            foreach (var key in keys) NativeMouse.KeyDown(key);
                            _heldKeys[binding.Id] = keys;
                        }
                        else if (_heldKeys.Remove(binding.Id, out var held))
                            foreach (var key in held.Reverse()) NativeMouse.KeyUp(key);
                    }
                    else if (command.Press) await MouseKeySequence.TapAsync(binding.Value, _actionCancel.Token);
                    break;
                case "Кнопка мыши":
                    var button = binding.Value;
                    if (binding.Trigger is "Удержание" or "Переключатель")
                    {
                        if (command.Press) { NativeMouse.MouseDown(button); _heldButtons[binding.Id] = button; }
                        else if (_heldButtons.Remove(binding.Id, out var held)) NativeMouse.MouseUp(held);
                    }
                    else if (command.Press) { NativeMouse.MouseDown(button); NativeMouse.MouseUp(button); }
                    break;
                case "Запустить сценарий":
                    if (command.Press) _startScenario();
                    else if (binding.Trigger is "Удержание" or "Переключатель") _stopScenario();
                    break;
                case "Остановить сценарий": if (command.Press) _stopScenario(); break;
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception error) { _message("Ошибка действия мыши: " + error.Message); }
        finally { _actionLock.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _actionCancel.Cancel();
        _timer.Stop();
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        if (_ownsGate) { _instanceGate?.ReleaseMutex(); _ownsGate = false; }
        _instanceGate?.Dispose();
        _instanceGate = null;
        foreach (var keys in _heldKeys.Values)
            foreach (var key in keys.Reverse()) NativeMouse.KeyUp(key);
        foreach (var button in _heldButtons.Values) NativeMouse.MouseUp(button);
        _heldKeys.Clear(); _heldButtons.Clear();
        _timer.Dispose();
    }
}

internal static class MouseKeySequence
{
    public static byte[] ParseChord(string text)
    {
        var keys = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NativeMouse.ParseKey).ToArray();
        if (keys.Length == 0 || keys.Length > 5) throw new ArgumentException("Введите до пяти клавиш через +, например Ctrl+C.");
        return keys;
    }

    public static async Task TapAsync(string sequence, CancellationToken cancel = default)
    {
        foreach (var part in sequence.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            cancel.ThrowIfCancellationRequested();
            if (part.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(part[..^2], out var delay) && delay is >= 0 and <= 10000)
            { await Task.Delay(delay, cancel); continue; }
            var keys = ParseChord(part);
            try { foreach (var key in keys) NativeMouse.KeyDown(key); }
            finally { foreach (var key in keys.Reverse()) NativeMouse.KeyUp(key); }
        }
    }
}
