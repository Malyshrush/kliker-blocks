using Microsoft.Win32;

namespace KlikerBlocks;

internal sealed record InstalledProgram(string Name, string Path)
{
    public override string ToString() => Name;
}

internal static class InstalledProgramCatalog
{
    internal static IReadOnlyList<InstalledProgram> Discover()
    {
        var found = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!File.Exists(path)) return;
            found.TryAdd(path, new InstalledProgram(name, path));
        }

        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        foreach (var (name, file) in new[]
        {
            (UiLanguage.T("Блокнот"), "notepad.exe"),
            (UiLanguage.T("Paint"), "mspaint.exe"),
            (UiLanguage.T("Проводник"), "explorer.exe"),
            (UiLanguage.T("Калькулятор"), "calc.exe"),
            (UiLanguage.T("Командная строка"), "cmd.exe")
        }) Add(name, Path.Combine(system, file));

        foreach (var location in new[] { Environment.SpecialFolder.Programs,
                     Environment.SpecialFolder.CommonPrograms })
        {
            var root = Environment.GetFolderPath(location);
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        Add(Path.GetFileNameWithoutExtension(file), file);
            }
            catch (UnauthorizedAccessException) { /* Other entries remain available. */ }
            catch (IOException) { /* A Start-menu entry may disappear during enumeration. */ }
        }

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var appPaths = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (appPaths is null) continue;
                foreach (var name in appPaths.GetSubKeyNames())
                {
                    using var entry = appPaths.OpenSubKey(name);
                    if (entry?.GetValue(null) is string path)
                        Add(Path.GetFileNameWithoutExtension(name), path);
                }
            }
            catch (System.Security.SecurityException) { /* Registry entries are optional. */ }
            catch (UnauthorizedAccessException) { /* Registry entries are optional. */ }
            catch (IOException) { /* Registry entries are optional. */ }
        }
        return found.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}

internal sealed class ProgramPickerForm : Form
{
    private readonly TextBox _search = new() { Dock = DockStyle.Top };
    private readonly ListBox _programs = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _path = new() { Dock = DockStyle.Bottom, Height = 46, AutoEllipsis = true };
    private readonly Button _choose = new() { Width = 112, Height = 34 };
    private IReadOnlyList<InstalledProgram> _all = [];

    internal string SelectedPath { get; private set; } = "";

    internal ProgramPickerForm()
    {
        Text = UiLanguage.T("Выбрать программу или файл");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(510, 390);
        Size = new Size(680, 520);
        var heading = new Label { Text = UiLanguage.T("Поиск в установленных программах и меню «Пуск»"),
            Dock = DockStyle.Top, Height = 28 };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Width = 112, Height = 34,
            DialogResult = DialogResult.Cancel };
        var browse = new Button { Text = UiLanguage.T("Обзор файлов…"), Width = 142, Height = 34 };
        _choose.Text = UiLanguage.T("Выбрать");
        _choose.Enabled = false;
        _choose.Click += (_, _) => AcceptSelection();
        browse.Click += (_, _) => BrowseFile();
        footer.Controls.Add(cancel);
        footer.Controls.Add(_choose);
        footer.Controls.Add(browse);
        Controls.Add(_programs);
        Controls.Add(_path);
        Controls.Add(footer);
        Controls.Add(_search);
        Controls.Add(heading);
        CancelButton = cancel;
        AcceptButton = _choose;
        _search.PlaceholderText = UiLanguage.T("Введите название программы…");
        _search.TextChanged += (_, _) => Filter();
        _programs.SelectedIndexChanged += (_, _) =>
        {
            _path.Text = (_programs.SelectedItem as InstalledProgram)?.Path ?? "";
            _choose.Enabled = _programs.SelectedItem is InstalledProgram;
        };
        _programs.DoubleClick += (_, _) => AcceptSelection();
        Shown += async (_, _) =>
        {
            _path.Text = UiLanguage.T("Поиск установленных программ…");
            try
            {
                _all = await Task.Run(InstalledProgramCatalog.Discover);
                if (!IsDisposed) Filter();
            }
            catch (Exception error)
            {
                if (!IsDisposed) _path.Text = UiLanguage.Format("Не удалось получить список программ: {0}", error.Message);
            }
        };
    }

    private void Filter()
    {
        var query = _search.Text.Trim();
        _programs.BeginUpdate();
        try
        {
            _programs.Items.Clear();
            _programs.Items.AddRange(_all.Where(item => query.Length == 0 ||
                item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Path.Contains(query, StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray());
        }
        finally { _programs.EndUpdate(); }
        _path.Text = UiLanguage.Format("Найдено программ: {0}", _programs.Items.Count);
    }

    private void AcceptSelection()
    {
        if (_programs.SelectedItem is not InstalledProgram item) return;
        SelectedPath = item.Path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseFile()
    {
        using var picker = new System.Windows.Forms.OpenFileDialog
        {
            Title = UiLanguage.T("Выберите программу или файл"),
            Filter = UiLanguage.Code == "en"
                ? "Programs and shortcuts (*.exe;*.bat;*.cmd;*.com;*.lnk)|*.exe;*.bat;*.cmd;*.com;*.lnk|All files (*.*)|*.*"
                : "Программы и ярлыки (*.exe;*.bat;*.cmd;*.com;*.lnk)|*.exe;*.bat;*.cmd;*.com;*.lnk|Все файлы (*.*)|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        SelectedPath = picker.FileName;
        DialogResult = DialogResult.OK;
        Close();
    }
}
