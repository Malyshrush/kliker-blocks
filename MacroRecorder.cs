using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal sealed class MacroOptions
{
    public string Name { get; set; } = "Макрос";
    public string Mode { get; set; } = "Клики и образы";
    public string RegionMode { get; set; } = "Вокруг клика";
    public string Shape { get; set; } = "Прямоугольник";
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 80;
    public int SearchPadding { get; set; } = 20;
    public Rectangle FixedArea { get; set; }
}

internal sealed class MacroSetupForm : Form
{
    private readonly TextBox _name = new() { Width = 330, Text = "Макрос" };
    private readonly ComboBox _mode = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _region = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _shape = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _width = new() { Width = 120, Minimum = 5, Maximum = 1000, Value = 80 };
    private readonly NumericUpDown _height = new() { Width = 120, Minimum = 5, Maximum = 1000, Value = 80 };
    private readonly NumericUpDown _padding = new() { Width = 120, Minimum = 0, Maximum = 1000, Value = 20 };
    public MacroOptions Options { get; } = new();

    public MacroSetupForm()
    {
        Text = "Запись макроса";
        Width = 440;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), AutoScroll = true };
        Controls.Add(panel);
        Add(panel, "Имя макроса", _name);
        _mode.Items.AddRange(["Клики и образы", "Все действия ввода",
            UiLanguage.T("Клики по элементам Windows")]);
        _mode.SelectedIndex = 0;
        Add(panel, "Режим записи", _mode);
        _region.Items.AddRange(["Вокруг клика", "Фиксированная область"]);
        _region.SelectedIndex = 0;
        Add(panel, "Область снимка при клике", _region);
        _shape.Items.AddRange(["Прямоугольник", "Квадрат", "Круг"]);
        _shape.SelectedIndex = 0;
        Add(panel, "Форма области вокруг клика", _shape);
        Add(panel, "Ширина области вокруг клика, px", _width);
        Add(panel, "Высота области вокруг клика, px", _height);
        Add(panel, "Дополнительная область поиска при воспроизведении, px", _padding);
        panel.Controls.Add(new Label { Width = 370, Height = 42, Text = "Фиксированную область можно выделить на экране после нажатия «Начать»." });
        var start = new Button { Text = "Начать запись", Width = 160, Height = 35 };
        start.Click += (_, _) => Confirm();
        panel.Controls.Add(start);
        _mode.SelectedIndexChanged += (_, _) =>
        {
            var enabled = _mode.SelectedIndex == 0;
            _region.Enabled = _shape.Enabled = _width.Enabled = _height.Enabled = _padding.Enabled = enabled;
        };
    }

    private static void Add(FlowLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, Width = 390, Height = 23 });
        panel.Controls.Add(control);
    }

    private void Confirm()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        { MessageBox.Show(this, "Задайте допустимое имя макроса."); return; }
        if (_mode.SelectedIndex == 1 && MessageBox.Show(this,
            "Полная запись сохраняет все нажатия клавиш, включая возможные пароли. Записывайте только свои действия. Продолжить?",
            "Конфиденциальность", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Options.Name = name;
        Options.Mode = _mode.SelectedIndex switch
        {
            1 => "Все действия ввода",
            2 => "Клики по элементам Windows",
            _ => "Клики и образы"
        };
        Options.RegionMode = _region.Text;
        Options.Shape = _shape.Text;
        Options.Width = (int)_width.Value;
        Options.Height = (int)_height.Value;
        Options.SearchPadding = (int)_padding.Value;
        DialogResult = DialogResult.OK;
    }
}

internal sealed class RecordingBar : Form
{
    private readonly Label _label = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly Button _pause = new() { Text = "Ⅱ Пауза", Width = 105, Height = 34 };
    private readonly Button _play = new() { Text = "▶ Продолжить", Width = 125, Height = 34, Enabled = false };
    private int _count;
    private bool _paused;
    private bool _allowClose;
    private readonly string _mode;
    public event Action? PauseRequested;
    public event Action? ResumeRequested;
    public event Action? StopRequested;
    public event Action? CloseRequested;

    public RecordingBar(string mode)
    {
        _mode = mode;
        Text = UiLanguage.T("Kliker Blocks — запись");
        Width = 460;
        Height = 132;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var screen = Screen.FromPoint(NativeMouse.Position()).WorkingArea;
        Location = new Point(screen.Right - Width - 20, screen.Top + 20);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(5) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        Controls.Add(layout);
        layout.Controls.Add(_label, 0, 0);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false };
        layout.Controls.Add(row, 0, 1);
        _pause.Click += (_, _) => PauseRequested?.Invoke();
        _play.Click += (_, _) => ResumeRequested?.Invoke();
        row.Controls.Add(_pause);
        row.Controls.Add(_play);
        var stop = new Button { Text = "■ Стоп", Width = 90, Height = 34 };
        stop.Click += (_, _) => StopRequested?.Invoke();
        row.Controls.Add(stop);
        var close = new Button { Text = "✕ Закрыть", Width = 100, Height = 34 };
        close.Click += (_, _) => CloseRequested?.Invoke();
        row.Controls.Add(close);
        FormClosing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            CloseRequested?.Invoke();
        };
        _timer.Tick += (_, _) => UpdateLabel();
        _timer.Start();
        UpdateLabel();
    }

    public void SetCount(int count) { _count = count; UpdateLabel(); }
    public void SetPaused(bool paused)
    {
        _paused = paused;
        _pause.Enabled = !paused;
        _play.Enabled = paused;
        if (paused) _clock.Stop(); else _clock.Start();
        UpdateLabel();
    }
    public void AllowClose() => _allowClose = true;
    private void UpdateLabel() => _label.Text = $"{_mode}: {_count} событий • {_clock.Elapsed:mm\\:ss}" + (_paused ? " • ПАУЗА" : " • ИДЁТ ЗАПИСЬ");
    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MacroRecorder : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct HookPoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData
    { public HookPoint Point; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyHookData
    { public uint VkCode; public uint ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly MacroOptions _options;
    private readonly AppSettings _settings;
    private readonly string _baseDirectory;
    private readonly Func<Rectangle> _ignoredArea;
    private readonly Action<int> _countChanged;
    private readonly Action<string> _error;
    private readonly Action _limitReached;
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyProc;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<MacroEvent> _events = [];
    private readonly List<CaptureAsset> _assets = [];
    private IntPtr _mouseHook;
    private IntPtr _keyHook;
    private long _lastEventMs;
    private long _lastMoveMs;
    private Point _lastMovePoint;
    private bool _limitNotified;
    private bool _stopped;
    private bool _paused;
    public IReadOnlyList<CaptureAsset> Assets => _assets;

    public MacroRecorder(MacroOptions options, AppSettings settings, string baseDirectory,
        Func<Rectangle> ignoredArea, Action<int> countChanged, Action<string> error, Action limitReached)
    {
        _options = options;
        _settings = settings;
        _baseDirectory = baseDirectory;
        _ignoredArea = ignoredArea;
        _countChanged = countChanged;
        _error = error;
        _limitReached = limitReached;
        _mouseProc = OnMouse;
        _keyProc = OnKey;
    }

    public void Start()
    {
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(14, _mouseProc, module, 0);
        if (_mouseHook == IntPtr.Zero) throw new InvalidOperationException("Не удалось включить запись мыши.");
        if (_options.Mode == "Все действия ввода")
        {
            _keyHook = SetWindowsHookEx(13, _keyProc, module, 0);
            if (_keyHook == IntPtr.Zero) { Dispose(); throw new InvalidOperationException("Не удалось включить запись клавиатуры."); }
        }
        _clock.Restart();
    }

    public MacroRecording Stop(bool hotkey = false)
    {
        Dispose();
        if (hotkey && _options.Mode == "Все действия ввода")
            while (_events.Count > 0 && _events[^1].Kind.StartsWith("Key", StringComparison.Ordinal) &&
                _events[^1].Key is "17" or "18" or "121" or "162" or "163" or "164" or "165")
                _events.RemoveAt(_events.Count - 1);
        return new MacroRecording { Name = _options.Name, Mode = _options.Mode,
            SearchPadding = _options.SearchPadding, Events = [.. _events] };
    }

    public void SetPaused(bool paused)
    {
        if (_stopped || _paused == paused) return;
        _paused = paused;
        if (paused) _clock.Stop(); else _clock.Start();
    }

    public void Dispose()
    {
        if (_stopped) return;
        _stopped = true;
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = IntPtr.Zero;
    }

    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !_stopped && !_paused)
            try
            {
                var info = Marshal.PtrToStructure<MouseHookData>(data);
                if ((info.Flags & 1) == 0)
                {
                    var point = new Point(info.Point.X, info.Point.Y);
                    if (!_ignoredArea().Contains(point)) HandleMouse(message.ToInt32(), point, info.MouseData);
                }
            }
            catch (Exception error) { _error("Ошибка записи мыши: " + error.Message); }
        return CallNextHookEx(_mouseHook, code, message, data);
    }

    private void HandleMouse(int message, Point point, uint data)
    {
        if (_options.Mode is "Клики и образы" or "Клики по элементам Windows")
        {
            var button = message switch { 0x0201 => "Левая", 0x0204 => "Правая", 0x0207 => "Средняя", _ => null };
            if (button == null) return;
            var entry = NewEvent("Click", point);
            entry.Button = button;
            try
            {
                if (_options.Mode == "Клики по элементам Windows" && button == "Левая")
                {
                    var element = UiAutomationTools.Inspect(point);
                    if (element.ControlType is "Button" or "MenuItem" or "Hyperlink" or
                        "ListItem" or "CheckBox" &&
                        (!string.IsNullOrWhiteSpace(element.AutomationId) ||
                         !string.IsNullOrWhiteSpace(element.Name)))
                    {
                        entry.WindowTitle = element.WindowTitle;
                        entry.AutomationId = element.AutomationId;
                        entry.ElementName = element.AutomationId.Length == 0 ? element.Name : "";
                        entry.ControlType = element.ControlType;
                    }
                }
                else if (_options.Mode == "Клики и образы")
                {
                    var area = CaptureArea(point);
                    var asset = CaptureService.Save(area, _options.Shape,
                        $"{_options.Name}_click_{_events.Count + 1:D3}", _settings, _baseDirectory);
                    _assets.Add(asset);
                    entry.CapturePath = asset.Path;
                    entry.CaptureX = area.X; entry.CaptureY = area.Y;
                    entry.CaptureWidth = area.Width; entry.CaptureHeight = area.Height;
                }
            }
            catch (Exception error) { _error(UiLanguage.T("Данные клика не получены: ") + error.Message); }
            Add(entry);
            return;
        }
        MacroEvent? eventToAdd = message switch
        {
            0x0200 => NewEvent("MouseMove", point),
            0x0201 or 0x0204 or 0x0207 => NewEvent("MouseDown", point),
            0x0202 or 0x0205 or 0x0208 => NewEvent("MouseUp", point),
            0x020A => NewEvent("Wheel", point),
            _ => null
        };
        if (eventToAdd == null) return;
        if (message == 0x0200)
        {
            var now = _clock.ElapsedMilliseconds;
            if (now - _lastMoveMs < 40 || (Math.Abs(point.X - _lastMovePoint.X) < 4 && Math.Abs(point.Y - _lastMovePoint.Y) < 4)) return;
            _lastMoveMs = now;
            _lastMovePoint = point;
        }
        eventToAdd.Button = message switch
        {
            0x0204 or 0x0205 => "Правая",
            0x0207 or 0x0208 => "Средняя",
            _ => "Левая"
        };
        if (message == 0x020A) eventToAdd.WheelSteps = (short)(data >> 16) / 120;
        Add(eventToAdd);
    }

    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !_stopped && !_paused)
            try
            {
                var info = Marshal.PtrToStructure<KeyHookData>(data);
                if ((info.Flags & 0x10) == 0)
                {
                    var kind = message.ToInt32() switch
                    {
                        0x0100 or 0x0104 => "KeyDown",
                        0x0101 or 0x0105 => "KeyUp",
                        _ => null
                    };
                    if (kind != null && info.VkCode is > 0 and <= 255)
                    {
                        var entry = NewEvent(kind, Point.Empty);
                        entry.Key = info.VkCode.ToString();
                        Add(entry);
                    }
                }
            }
            catch (Exception error) { _error("Ошибка записи клавиатуры: " + error.Message); }
        return CallNextHookEx(_keyHook, code, message, data);
    }

    private MacroEvent NewEvent(string kind, Point point) => new()
    {
        Kind = kind, X = point.X, Y = point.Y,
        DelayMs = (int)Math.Clamp(_clock.ElapsedMilliseconds - _lastEventMs, 0, 3_600_000)
    };

    private void Add(MacroEvent entry)
    {
        if (_events.Count >= 500)
        {
            if (!_limitNotified) { _limitNotified = true; _limitReached(); }
            return;
        }
        _events.Add(entry);
        _lastEventMs = _clock.ElapsedMilliseconds;
        _countChanged(_events.Count);
        if (_events.Count == 500 && !_limitNotified) { _limitNotified = true; _limitReached(); }
    }

    private Rectangle CaptureArea(Point point)
    {
        var screen = SystemInformation.VirtualScreen;
        var width = _options.Shape == "Прямоугольник" ? _options.Width : Math.Min(_options.Width, _options.Height);
        var height = _options.Shape == "Прямоугольник" ? _options.Height : width;
        var area = _options.RegionMode == "Фиксированная область"
            ? _options.FixedArea
            : new Rectangle(point.X - width / 2, point.Y - height / 2, width, height);
        area.Intersect(screen);
        if (area.Width < 5 || area.Height < 5) throw new ArgumentException("Область клика вне экрана.");
        return area;
    }
}
