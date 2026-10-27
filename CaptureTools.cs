using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed class AppSettings
{
    public string CoordinateHotkey { get; set; } = "Ctrl+Alt+Q";
    public string ScreenshotHotkey { get; set; } = "Ctrl+Alt+P";
    public string ScreenshotFolder { get; set; } = "Screenshots";
    public string ScreenshotPrefix { get; set; } = "capture";
    public string UiLanguageCode { get; set; } = "en";
    public bool InitialLanguageChosen { get; set; }
    public bool JournalWordWrap { get; set; } = true;
    public string ArgosPythonPath { get; set; } = "Встроенный";
    public string TranslationEndpoint { get; set; } = "http://127.0.0.1:5000/translate";
    public Dictionary<string, BlockAppearance> BlockAppearanceDefaults { get; set; } = [];

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KLIKER Blocks", "settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings(); }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal static class HotkeyParser
{
    public static (uint Modifiers, uint Key) Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Укажите клавишу, например F8 или Ctrl+Alt+Q.");
        uint modifiers = 0;
        foreach (var part in parts[..^1])
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => 0x0002u,
                "ALT" => 0x0001u,
                "SHIFT" => 0x0004u,
                "WIN" or "WINDOWS" => 0x0008u,
                _ => throw new ArgumentException($"Неизвестный модификатор: {part}")
            };
        var keyName = parts[^1].Trim().ToUpperInvariant();
        uint key;
        if (keyName.Length == 1 && keyName[0] is >= 'A' and <= 'Z') key = keyName[0];
        else if (keyName.Length == 1 && keyName[0] is >= '0' and <= '9') key = (uint)(0x30 + keyName[0] - '0');
        else if (keyName.StartsWith('F') && int.TryParse(keyName[1..], out var f) && f is >= 1 and <= 24) key = (uint)(0x6F + f);
        else key = keyName switch
        {
            "SPACE" => 0x20, "ENTER" => 0x0D, "TAB" => 0x09, "ESC" or "ESCAPE" => 0x1B,
            "INSERT" => 0x2D, "DELETE" => 0x2E, "HOME" => 0x24, "END" => 0x23,
            "PAGEUP" => 0x21, "PAGEDOWN" => 0x22, "UP" => 0x26, "DOWN" => 0x28,
            "LEFT" => 0x25, "RIGHT" => 0x27,
            _ => throw new ArgumentException($"Неизвестная клавиша: {keyName}")
        };
        return (modifiers | 0x4000u, key); // MOD_NOREPEAT
    }
}

internal sealed class HotkeySettingsForm : Form
{
    private readonly TextBox _coordinates = new() { Width = 320 };
    private readonly TextBox _screenshot = new() { Width = 320 };
    private readonly TextBox _folder = new() { Width = 320 };
    private readonly TextBox _prefix = new() { Width = 320 };
    public AppSettings Settings { get; }

    public HotkeySettingsForm(AppSettings settings)
    {
        Settings = new AppSettings
        {
            CoordinateHotkey = settings.CoordinateHotkey,
            ScreenshotHotkey = settings.ScreenshotHotkey,
            ScreenshotFolder = settings.ScreenshotFolder,
            ScreenshotPrefix = settings.ScreenshotPrefix,
            UiLanguageCode = settings.UiLanguageCode,
            InitialLanguageChosen = settings.InitialLanguageChosen,
            JournalWordWrap = settings.JournalWordWrap,
            ArgosPythonPath = settings.ArgosPythonPath,
            TranslationEndpoint = settings.TranslationEndpoint
        };
        Text = "Горячие клавиши и снимки";
        Width = 450;
        Height = 340;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), AutoScroll = true };
        Controls.Add(panel);
        AddField(panel, "Сохранить координаты (например F8, Ctrl+Alt+Q)", _coordinates, settings.CoordinateHotkey);
        AddField(panel, "Снимок области (например F9, Ctrl+Alt+P)", _screenshot, settings.ScreenshotHotkey);
        AddField(panel, "Папка снимков (абсолютная или от проекта)", _folder, settings.ScreenshotFolder);
        AddField(panel, "Автоматический префикс имени", _prefix, settings.ScreenshotPrefix);
        var save = new Button { Text = "Сохранить", Width = 130, Height = 34, Margin = new Padding(0, 12, 0, 0) };
        save.Click += (_, _) => SaveValues();
        panel.Controls.Add(save);
    }

    private static void AddField(FlowLayoutPanel panel, string label, TextBox box, string value)
    {
        panel.Controls.Add(new Label { Text = label, Width = 390, Height = 24 });
        box.Text = value;
        panel.Controls.Add(box);
    }

    private void SaveValues()
    {
        try
        {
            var coordinate = HotkeyParser.Parse(_coordinates.Text);
            var screenshot = HotkeyParser.Parse(_screenshot.Text);
            if (coordinate == screenshot) throw new ArgumentException("Для координат и снимков нужны разные горячие клавиши.");
            if (string.IsNullOrWhiteSpace(_folder.Text)) throw new ArgumentException("Укажите папку снимков.");
            if (string.IsNullOrWhiteSpace(_prefix.Text)) throw new ArgumentException("Укажите префикс имени.");
            Settings.CoordinateHotkey = _coordinates.Text.Trim();
            Settings.ScreenshotHotkey = _screenshot.Text.Trim();
            Settings.ScreenshotFolder = _folder.Text.Trim();
            Settings.ScreenshotPrefix = _prefix.Text.Trim();
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Проверьте настройки"); }
    }
}

internal sealed class CaptureOverlay : Form
{
    private readonly FlowLayoutPanel _bar = new() { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(35, 42, 55) };
    private readonly TextBox _name = new() { Width = 250, PlaceholderText = "Имя (пусто = автоматически)" };
    private Point? _start;
    private Point _end;
    public string Shape { get; private set; } = "Прямоугольник";
    public string RequestedName => _name.Text.Trim();
    public Rectangle SelectedArea { get; private set; }

    public CaptureOverlay()
    {
        Text = UiLanguage.T("Kliker Blocks — выбор области");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(30, 35, 45);
        Opacity = 0.68;
        Cursor = Cursors.Cross;
        Controls.Add(_bar);
        foreach (var shape in new[] { "Прямоугольник", "Квадрат", "Круг" })
        {
            var button = new Button { Text = shape, AutoSize = true, Height = 34, Margin = new Padding(6, 8, 0, 0) };
            button.Click += (_, _) => { Shape = shape; Invalidate(); };
            _bar.Controls.Add(button);
        }
        _name.Margin = new Padding(18, 12, 0, 0);
        _bar.Controls.Add(_name);
        _bar.Controls.Add(new Label { Text = "Перетащите область • Esc — отмена", AutoSize = true, ForeColor = Color.White, Margin = new Padding(16, 17, 0, 0) });
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            if (_name.Focused) return;
            if (e.KeyCode == Keys.R) Shape = "Прямоугольник";
            if (e.KeyCode == Keys.S) Shape = "Квадрат";
            if (e.KeyCode == Keys.C) Shape = "Круг";
            Invalidate();
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.Y < _bar.Height) return;
        _start = e.Location;
        _end = e.Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_start == null) return;
        _end = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_start == null) return;
        _end = e.Location;
        Capture = false;
        var area = Selection();
        _start = null;
        if (area.Width < 5 || area.Height < 5) { Invalidate(); return; }
        SelectedArea = new Rectangle(area.X + Bounds.Left, area.Y + Bounds.Top, area.Width, area.Height);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_start == null) return;
        var area = Selection();
        using var pen = new Pen(Color.DeepSkyBlue, 3) { DashStyle = DashStyle.Dash };
        if (Shape == "Круг") e.Graphics.DrawEllipse(pen, area);
        else e.Graphics.DrawRectangle(pen, area);
        using var font = new Font(Font.FontFamily, 12, FontStyle.Bold);
        e.Graphics.DrawString($"{Shape}: {area.Width}×{area.Height}", font, Brushes.White, area.X, Math.Max(_bar.Height + 3, area.Y - 26));
    }

    private Rectangle Selection()
    {
        if (_start is not Point start) return Rectangle.Empty;
        var dx = _end.X - start.X;
        var dy = _end.Y - start.Y;
        if (Shape != "Прямоугольник")
        {
            var side = Math.Min(Math.Abs(dx), Math.Abs(dy));
            dx = Math.Sign(dx) * side;
            dy = Math.Sign(dy) * side;
        }
        return Rectangle.FromLTRB(Math.Min(start.X, start.X + dx), Math.Min(start.Y, start.Y + dy),
            Math.Max(start.X, start.X + dx), Math.Max(start.Y, start.Y + dy));
    }
}

internal static class CaptureService
{
    public static CaptureAsset Save(Rectangle area, string shape, string name, AppSettings settings, string baseDirectory)
    {
        var folder = ExternalTools.ResolvePath(settings.ScreenshotFolder, baseDirectory);
        Directory.CreateDirectory(folder);
        var stem = string.IsNullOrWhiteSpace(name)
            ? settings.ScreenshotPrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
            : Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(stem) || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Недопустимое имя снимка.");
        var path = Path.Combine(folder, stem + ".png");
        for (var index = 2; File.Exists(path); index++) path = Path.Combine(folder, $"{stem}_{index}.png");
        using var screenshot = ScreenTools.Capture(area);
        if (shape == "Круг")
        {
            using var mask = new Bitmap(screenshot.Width, screenshot.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(mask))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var region = new GraphicsPath();
                region.AddEllipse(0, 0, screenshot.Width - 1, screenshot.Height - 1);
                graphics.SetClip(region);
                graphics.DrawImageUnscaled(screenshot, 0, 0);
            }
            mask.Save(path, ImageFormat.Png);
        }
        else screenshot.Save(path, ImageFormat.Png);
        return new CaptureAsset { Name = Path.GetFileNameWithoutExtension(path), Path = path, Shape = shape,
            X = area.X, Y = area.Y, Width = area.Width, Height = area.Height };
    }
}
