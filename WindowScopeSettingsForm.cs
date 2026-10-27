namespace KlikerBlocks;

internal sealed class WindowScopeSettingsForm : Form
{
    private readonly CheckBox _enabled = new() { Text = "Ограничить сценарий выбранным окном или программой", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
    private readonly TextBox _process = new() { Width = 350 };
    private readonly TextBox _title = new() { Width = 620 };
    private readonly TextBox _handle = new() { Width = 250 };
    private readonly ListBox _windows = new() { Width = 650, Height = 170, HorizontalScrollbar = true };
    private int _processId;

    public WindowScopeSettings Settings { get; private set; }

    public WindowScopeSettingsForm(WindowScopeSettings? current)
    {
        Settings = (current ?? new WindowScopeSettings()).Copy();
        Text = "Привязка сценария к окну";
        Width = 740;
        Height = 700;
        MinimumSize = new Size(660, 560);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(root);
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
        root.Controls.Add(content, 0, 0);
        _enabled.Checked = Settings.Enabled;
        content.Controls.Add(_enabled);
        content.Controls.Add(new Label
        {
            Text = "Когда привязка выключена, сценарий работает как раньше. Когда включена, он ждёт нужное окно и перед вводом выводит его на передний план. Переключение фокуса может прерывать вашу работу в других программах.",
            Width = 650, Height = 70
        });

        content.Controls.Add(new Label { Text = "Открытые окна — выберите для заполнения полей", Width = 650, Height = 25 });
        content.Controls.Add(_windows);
        var refresh = new Button { Text = "Обновить список", Width = 150, Height = 32 };
        refresh.Click += (_, _) => RefreshWindows();
        content.Controls.Add(refresh);
        _windows.SelectedIndexChanged += (_, _) =>
        {
            if (_windows.SelectedItem is not WindowCandidate selected) return;
            _process.Text = selected.ProcessName;
            _title.Text = selected.Title;
            _handle.Text = $"0x{selected.Handle.ToInt64():X}";
            _processId = selected.ProcessId;
        };

        AddField(content, "Как находить окно", _mode);
        _mode.Items.AddRange(["Процесс", "Процесс и заголовок", "Заголовок", "HWND"]);
        _mode.SelectedItem = _mode.Items.Contains(Settings.MatchMode) ? Settings.MatchMode : "Процесс";
        AddField(content, "Имя процесса, например WINWORD или EXCEL", _process);
        _process.Text = Settings.ProcessName;
        AddField(content, "Часть заголовка окна", _title);
        _title.Text = Settings.TitleContains;
        AddField(content, "Код HWND (десятичный или 0x в шестнадцатеричном виде)", _handle);
        _handle.Text = Settings.WindowHandle > 0 ? $"0x{Settings.WindowHandle:X}" : "";
        _processId = Settings.ProcessId;
        content.Controls.Add(new Label
        {
            Text = "Поиск по процессу переживает перезапуск программы. HWND действует только для конкретного открытого окна: после закрытия его код меняется. Для действий мыши окно должно стать активным; Kliker Blocks попытается переключиться на него автоматически.",
            Width = 650, Height = 75
        });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(12) };
        var apply = new Button { Text = "Применить", Width = 110, Height = 34 };
        apply.Click += (_, _) => SaveValues();
        var cancel = new Button { Text = "Отмена", Width = 95, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 1);
        AcceptButton = apply;
        CancelButton = cancel;
        RefreshWindows();
    }

    private static void AddField(FlowLayoutPanel content, string label, Control input)
    {
        content.Controls.Add(new Label { Text = label, Width = 650, Height = 26,
            Margin = new Padding(0, 12, 0, 0) });
        content.Controls.Add(input);
    }

    private void RefreshWindows()
    {
        _windows.BeginUpdate();
        try
        {
            _windows.Items.Clear();
            foreach (var window in WindowTools.ListOpenWindows()) _windows.Items.Add(window);
        }
        finally { _windows.EndUpdate(); }
    }

    private void SaveValues()
    {
        try
        {
            var handleText = _handle.Text.Trim();
            var mode = _mode.SelectedItem?.ToString() ?? "Процесс";
            var handle = Settings.WindowHandle;
            if (_enabled.Checked && mode == "HWND")
            {
                handle = handleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt64(handleText[2..], 16)
                    : long.Parse(handleText);
            }
            var next = new WindowScopeSettings
            {
                Enabled = _enabled.Checked,
                MatchMode = mode,
                ProcessName = _process.Text.Trim(),
                TitleContains = _title.Text.Trim(),
                WindowHandle = handle,
                ProcessId = _processId
            };
            var error = WindowScopeGuard.Validate(next);
            if (error is not null) throw new ArgumentException(error);
            if (next.Enabled && next.MatchMode == "HWND")
            {
                var candidate = WindowTools.GetWindow(new IntPtr(next.WindowHandle));
                if (candidate is null) throw new ArgumentException("Окно с указанным HWND сейчас не найдено.");
                next.ProcessId = candidate.ProcessId;
            }
            Settings = next;
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            MessageBox.Show(this, error.Message, "Проверьте привязку", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
