using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed record DisplayCapability(string Name, int X, int Y, int Width, int Height, bool Primary);

internal sealed class ComputerCapabilities
{
    public int SchemaVersion { get; init; } = 1;
    public DateTime CollectedAtUtc { get; init; }
    public string WindowsVersion { get; init; } = "";
    public string OsArchitecture { get; init; } = "";
    public string ProcessArchitecture { get; init; } = "";
    public string DotNetVersion { get; init; } = "";
    public string UiCulture { get; init; } = "";
    public string[] KeyboardLanguages { get; init; } = [];
    public SpeechVoice[] Voices { get; init; } = [];
    public DisplayCapability[] Displays { get; init; } = [];

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KLIKER Blocks", "computer-capabilities.json");
    public static string SavedFilePath { get; private set; } = FilePath;

    public static ComputerCapabilities? Current { get; private set; }

    public static ComputerCapabilities Capture() => new()
    {
        CollectedAtUtc = DateTime.UtcNow,
        WindowsVersion = RuntimeInformation.OSDescription,
        OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        DotNetVersion = RuntimeInformation.FrameworkDescription,
        UiCulture = CultureInfo.CurrentUICulture.Name,
        KeyboardLanguages = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>()
            .Select(language => language.Culture.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        Voices = WindowsSpeech.InstalledVoices().ToArray(),
        Displays = Screen.AllScreens.Select(display => new DisplayCapability(display.DeviceName,
            display.Bounds.X, display.Bounds.Y, display.Bounds.Width, display.Bounds.Height,
            display.Primary)).ToArray()
    };

    public static void Refresh(string? path = null)
    {
        var snapshot = Capture();
        if (path is null)
        {
            var preferred = SavedFilePath;
            try { Save(snapshot, preferred); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Save(snapshot, Path.Combine(AppContext.BaseDirectory, "computer-capabilities.json"));
            }
        }
        else Save(snapshot, path);
        Current = snapshot;
    }

    private static void Save(ComputerCapabilities snapshot, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + ".new";
        File.WriteAllText(staging, JsonSerializer.Serialize(snapshot,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(staging, path, overwrite: true);
        SavedFilePath = path;
    }

    public static void Initialize()
    {
        try { Refresh(); }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine("Computer capability inventory could not be saved: " + error.Message);
            try { Current = JsonSerializer.Deserialize<ComputerCapabilities>(File.ReadAllText(SavedFilePath)); }
            catch { Current = null; }
        }
    }
}

internal sealed class ComputerCapabilitiesForm : Form
{
    private readonly TextBox _details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10) };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };

    public ComputerCapabilitiesForm()
    {
        Text = UiLanguage.T("Возможности компьютера и голоса");
        Width = 760; Height = 560; MinimumSize = new Size(510, 380);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        Controls.Add(root);
        root.Controls.Add(_details, 0, 0);
        root.Controls.Add(_status, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = UiLanguage.T("Закрыть"), DialogResult = DialogResult.OK,
            Width = 115, Height = 36 };
        var refresh = new Button { Text = UiLanguage.T("Обновить данные"), Width = 150, Height = 36 };
        refresh.Click += (_, _) =>
        {
            try { ComputerCapabilities.Refresh(); ShowSnapshot(); }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttons.Controls.Add(close); buttons.Controls.Add(refresh);
        root.Controls.Add(buttons, 0, 2);
        AcceptButton = close;
        ShowSnapshot();
    }

    private void ShowSnapshot()
    {
        var value = ComputerCapabilities.Current ?? ComputerCapabilities.Capture();
        var result = new System.Text.StringBuilder();
        result.AppendLine(UiLanguage.Format("Собрано (UTC): {0}", value.CollectedAtUtc.ToString("u")));
        result.AppendLine(UiLanguage.Format("Windows: {0}", value.WindowsVersion));
        result.AppendLine(UiLanguage.Format("Архитектура ОС / процесса: {0} / {1}",
            value.OsArchitecture, value.ProcessArchitecture));
        result.AppendLine(UiLanguage.Format("Платформа .NET: {0}", value.DotNetVersion));
        result.AppendLine(UiLanguage.Format("Язык интерфейса Windows: {0}", value.UiCulture));
        result.AppendLine(UiLanguage.Format("Клавиатуры: {0}", string.Join(", ", value.KeyboardLanguages)));
        result.AppendLine().AppendLine(UiLanguage.T("Голоса Windows:"));
        foreach (var voice in value.Voices) result.AppendLine("  " + voice.Name + " [" + voice.Language + "]");
        if (value.Voices.Length == 0) result.AppendLine("  " + UiLanguage.T("Голоса не найдены."));
        result.AppendLine().AppendLine(UiLanguage.T("Экраны:"));
        foreach (var display in value.Displays)
            result.AppendLine($"  {display.Name}: {display.Width}x{display.Height} @ {display.X},{display.Y}" +
                (display.Primary ? " (" + UiLanguage.T("основной") + ")" : ""));
        _details.Text = result.ToString();
        _status.Text = UiLanguage.Format("Локальный файл: {0}", ComputerCapabilities.SavedFilePath);
    }
}
