namespace KlikerBlocks;

internal sealed class ScenarioAutomationForm : Form
{
    private readonly AppSettings _settings;
    private readonly CheckBox _windowsStart = new() { AutoSize = true };
    private readonly CheckBox _startScenario = new() { AutoSize = true };
    private readonly TextBox _startupPath = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _startupDelay = new() { Minimum = 0, Maximum = 3600, Width = 110 };
    private readonly ListBox _schedules = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    internal ScenarioAutomationForm(AppSettings settings)
    {
        _settings = settings;
        Text = UiLanguage.T("Автозапуск и расписание сценариев");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 540);
        Size = new Size(830, 610);
        AutoScaleMode = AutoScaleMode.Font;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 215));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);
        var startup = new GroupBox { Text = UiLanguage.T("При входе в Windows"), Dock = DockStyle.Fill };
        root.Controls.Add(startup, 0, 0);
        var startupGrid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8),
            RowCount = 4, ColumnCount = 3 };
        startupGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        startupGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        startupGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        for (var i = 0; i < 4; i++) startupGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        startup.Controls.Add(startupGrid);
        _windowsStart.Text = UiLanguage.T("Запускать Kliker Blocks");
        try { _windowsStart.Checked = AutostartSettings.IsEnabled(); }
        catch { _windowsStart.Checked = false; }
        startupGrid.Controls.Add(_windowsStart, 0, 0);
        _startScenario.Text = UiLanguage.T("Запускать сценарий");
        _startScenario.Checked = settings.StartupScenarioEnabled;
        startupGrid.Controls.Add(_startScenario, 1, 0);
        startupGrid.Controls.Add(new Label { Text = UiLanguage.T("Файл сценария .klb"), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _startupPath.Text = settings.StartupScenarioPath;
        startupGrid.Controls.Add(_startupPath, 1, 1);
        var browse = new Button { Text = UiLanguage.T("Обзор…"), Dock = DockStyle.Fill };
        browse.Click += (_, _) => Browse(_startupPath);
        startupGrid.Controls.Add(browse, 2, 1);
        startupGrid.Controls.Add(new Label { Text = UiLanguage.T("Задержка после входа, секунд"), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        _startupDelay.Value = Math.Clamp(settings.StartupScenarioDelaySeconds, 0, 3600);
        startupGrid.Controls.Add(_startupDelay, 1, 2);
        var saveStartup = new Button { Text = UiLanguage.T("Сохранить"), Dock = DockStyle.Fill };
        saveStartup.Click += (_, _) => SaveStartup();
        startupGrid.Controls.Add(saveStartup, 2, 2);
        var note = new Label { Text = UiLanguage.T("Сценарий запускается только после входа в Windows; программа может стартовать сразу или после задержки."),
            Dock = DockStyle.Fill, AutoEllipsis = false, ForeColor = Color.FromArgb(65, 78, 98) };
        startupGrid.SetColumnSpan(note, 3);
        startupGrid.Controls.Add(note, 0, 3);

        var scheduled = new GroupBox { Text = UiLanguage.T("Запуск по расписанию"), Dock = DockStyle.Fill };
        root.Controls.Add(scheduled, 0, 1);
        var scheduleGrid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8),
            RowCount = 2, ColumnCount = 1 };
        scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        scheduled.Controls.Add(scheduleGrid);
        scheduleGrid.Controls.Add(_schedules, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        scheduleGrid.Controls.Add(actions, 0, 1);
        AddAction(actions, "Добавить…", () => EditSchedule(null));
        AddAction(actions, "Изменить…", () => EditSchedule(_schedules.SelectedItem as ScenarioSchedule));
        AddAction(actions, "Включить / выключить", ToggleSchedule);
        AddAction(actions, "Удалить", RemoveSchedule);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.Controls.Add(new Label { Text = UiLanguage.T("Расписание Windows открывает отдельное окно программы для каждого запуска. Для запуска пользователь должен войти в Windows."),
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(65, 78, 98) }, 0, 0);
        var close = new Button { Text = UiLanguage.T("Закрыть"), Dock = DockStyle.Fill, DialogResult = DialogResult.OK };
        footer.Controls.Add(close, 1, 0);
        root.Controls.Add(footer, 0, 2);
        RefreshSchedules();
    }

    private static void AddAction(FlowLayoutPanel panel, string label, Action action)
    {
        var button = new Button { Text = UiLanguage.T(label), AutoSize = true, MinimumSize = new Size(120, 34), Margin = new Padding(0, 4, 8, 0) };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }

    private void SaveStartup()
    {
        try
        {
            var path = _startupPath.Text.Trim();
            if (_startScenario.Checked && !File.Exists(path)) throw new FileNotFoundException(
                UiLanguage.T("Файл сценария не найден."), path);
            AutostartSettings.SetEnabled(_windowsStart.Checked);
            _settings.StartupScenarioEnabled = _startScenario.Checked;
            _settings.StartupScenarioPath = path;
            _settings.StartupScenarioDelaySeconds = (int)_startupDelay.Value;
            _settings.Save();
            MessageBox.Show(this, UiLanguage.T("Настройки автозапуска сохранены."), Text);
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void EditSchedule(ScenarioSchedule? existing)
    {
        using var dialog = new ScenarioScheduleEditForm(existing);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var item = dialog.Result;
        try
        {
            ScenarioScheduling.Register(item, Application.ExecutablePath);
            if (existing is null) _settings.ScenarioSchedules.Add(item);
            else
            {
                var index = _settings.ScenarioSchedules.FindIndex(schedule => schedule.Id == existing.Id);
                if (index >= 0) _settings.ScenarioSchedules[index] = item;
            }
            _settings.Save();
            RefreshSchedules();
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ToggleSchedule()
    {
        if (_schedules.SelectedItem is not ScenarioSchedule schedule) return;
        try
        {
            if (schedule.Enabled) ScenarioScheduling.Delete(schedule.Id);
            else ScenarioScheduling.Register(schedule, Application.ExecutablePath);
            schedule.Enabled = !schedule.Enabled;
            _settings.Save();
            RefreshSchedules();
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RemoveSchedule()
    {
        if (_schedules.SelectedItem is not ScenarioSchedule schedule) return;
        try
        {
            if (schedule.Enabled) ScenarioScheduling.Delete(schedule.Id);
            _settings.ScenarioSchedules.RemoveAll(item => item.Id == schedule.Id);
            _settings.Save();
            RefreshSchedules();
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RefreshSchedules()
    {
        _schedules.Items.Clear();
        foreach (var schedule in _settings.ScenarioSchedules.OrderBy(item => item.StartLocal)) _schedules.Items.Add(schedule);
        _schedules.DisplayMember = nameof(ScenarioSchedule.Description);
    }

    internal static void Browse(TextBox target)
    {
        using var dialog = new OpenFileDialog { Filter = "Kliker Blocks (*.klb)|*.klb" };
        if (dialog.ShowDialog() == DialogResult.OK) target.Text = dialog.FileName;
    }
}

internal sealed class ScenarioScheduleEditForm : Form
{
    private readonly TextBox _path = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dateTime = new() { Dock = DockStyle.Fill,
        Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", ShowUpDown = true };
    private readonly ComboBox _repeat = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Guid _id;
    internal ScenarioSchedule Result { get; private set; } = new();

    internal ScenarioScheduleEditForm(ScenarioSchedule? existing)
    {
        _id = existing?.Id ?? Guid.NewGuid();
        Text = UiLanguage.T(existing is null ? "Новое расписание" : "Изменить расписание");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 260);
        Size = new Size(700, 270);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 4 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        for (var i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(grid);
        grid.Controls.Add(Label("Файл сценария .klb"), 0, 0);
        _path.Text = existing?.ProjectPath ?? "";
        grid.Controls.Add(_path, 1, 0);
        var browse = new Button { Text = UiLanguage.T("Обзор…"), Dock = DockStyle.Fill };
        browse.Click += (_, _) => ScenarioAutomationForm.Browse(_path);
        grid.Controls.Add(browse, 2, 0);
        grid.Controls.Add(Label("Дата и время (до секунды)"), 0, 1);
        _dateTime.Value = existing?.StartLocal ?? DateTime.Now.AddMinutes(5);
        grid.Controls.Add(_dateTime, 1, 1);
        grid.Controls.Add(Label("Повтор"), 0, 2);
        _repeat.Items.AddRange([UiLanguage.T("Один раз"), UiLanguage.T("Ежедневно")]);
        _repeat.SelectedIndex = existing?.Repeat == "Daily" ? 1 : 0;
        grid.Controls.Add(_repeat, 1, 2);
        var save = new Button { Text = UiLanguage.T("Сохранить"), Dock = DockStyle.Fill };
        save.Click += (_, _) => Save();
        grid.Controls.Add(save, 2, 3);
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        grid.Controls.Add(cancel, 1, 3);
    }

    private static Label Label(string title) => new() { Text = UiLanguage.T(title), Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft };

    private void Save()
    {
        var path = _path.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this, UiLanguage.T("Файл сценария не найден."), Text);
            return;
        }
        Result = new ScenarioSchedule { Id = _id, ProjectPath = path, StartLocal = _dateTime.Value,
            Repeat = _repeat.SelectedIndex == 1 ? "Daily" : "Once", Enabled = true };
        DialogResult = DialogResult.OK;
    }
}
