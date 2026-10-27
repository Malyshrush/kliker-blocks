namespace KlikerBlocks;

internal sealed class JournalSettingsForm : Form
{
    private readonly CheckBox _enabled;
    private readonly TextBox _name;

    public bool WriteToFile => _enabled.Checked;
    public string FileName => _name.Text.Trim();

    public static bool IsValidJournalName(string name, string? projectFile) =>
        ScriptLogWriter.IsValidName(name) &&
        (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) &&
        !name.Equals(Path.GetFileName(projectFile), StringComparison.OrdinalIgnoreCase);

    public JournalSettingsForm(bool enabled, string fileName, string? projectFile)
    {
        Text = UiLanguage.T("Запись журнала в текстовый файл");
        Width = 650;
        Height = 350;
        MinimumSize = new Size(650, 350);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
            ColumnCount = 1, RowCount = 6 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(panel);

        _enabled = new CheckBox { Text = UiLanguage.T("Записывать журнал при запуске сценария"), Checked = enabled,
            Dock = DockStyle.Fill };
        panel.Controls.Add(_enabled, 0, 0);
        panel.Controls.Add(new Label { Text = UiLanguage.T("Имя файла UTF-8 (.txt или .log):"), Dock = DockStyle.Fill }, 0, 1);
        _name = new TextBox { Text = fileName, Dock = DockStyle.Fill };
        panel.Controls.Add(_name, 0, 2);
        var pathLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true,
            ForeColor = Color.FromArgb(35, 78, 125) };
        void ShowPath() => pathLabel.Text = UiLanguage.T("Полный путь к журналу:") + Environment.NewLine +
            Path.Combine(projectFile == null ? AppContext.BaseDirectory : Path.GetDirectoryName(projectFile)!,
                string.IsNullOrWhiteSpace(_name.Text) ? "*.log" : _name.Text.Trim());
        _name.TextChanged += (_, _) => ShowPath();
        ShowPath();
        panel.Controls.Add(pathLabel, 0, 3);
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = false,
            Text = UiLanguage.T("Файл находится рядом с сохранённым проектом; для несохранённого — рядом с программой. При повторных запусках записи дописываются. Максимальный размер — 8 МиБ."),
            ForeColor = Color.FromArgb(65, 75, 92) }, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var apply = new Button { Text = UiLanguage.T("Применить"), Width = 105, Height = 32 };
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        panel.Controls.Add(buttons, 0, 5);
        AcceptButton = apply;
        CancelButton = cancel;
        apply.Click += (_, _) =>
        {
            if (WriteToFile && !IsValidJournalName(FileName, projectFile))
            {
                MessageBox.Show(this, UiLanguage.T("Введите имя файла .txt или .log без пути, отличное от имени проекта (до 160 символов)."),
                    UiLanguage.T("Файл журнала"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
    }
}
