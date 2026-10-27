using System.Drawing;

namespace KlikerBlocks;

internal sealed class CompactControlForm : Form
{
    private readonly Label _scenario = new() { AutoEllipsis = true, Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Label _state = new() { AutoEllipsis = true, Dock = DockStyle.Fill };
    private readonly Label _window = new() { AutoEllipsis = true, Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(80, 90, 102) };
    private readonly Button _start = new() { Text = UiLanguage.T("▶ Пуск"), Width = 90, Height = 31, Margin = new Padding(2) };
    private readonly Button _pause = new() { Text = UiLanguage.T("⏸ Пауза"), Width = 95, Height = 31, Margin = new Padding(2) };
    private readonly Button _stop = new() { Text = UiLanguage.T("■ Стоп"), Width = 75, Height = 31, Margin = new Padding(2) };
    private bool _allowClose;

    public CompactControlForm(Action showEditor, Action start, Action togglePause, Action stop,
        Action open, Action save, Action settings, Action mouseSettings, Action help, Action exit)
    {
        Text = "Kliker Blocks — " + UiLanguage.T("управление");
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 465;
        Height = 210;
        MinimumSize = new Size(440, 195);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        Controls.Add(root);

        var menu = new MenuStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };
        root.Controls.Add(menu, 0, 0);
        var fileMenu = new ToolStripMenuItem(UiLanguage.T("Файл"));
        fileMenu.DropDownItems.Add(UiLanguage.T("Открыть проект…"), null, (_, _) => open());
        fileMenu.DropDownItems.Add(UiLanguage.T("Сохранить проект…"), null, (_, _) => save());
        fileMenu.DropDownItems.Add(UiLanguage.T("Выход из Kliker Blocks"), null, (_, _) => exit());
        var scenarioMenu = new ToolStripMenuItem(UiLanguage.T("Сценарий"));
        scenarioMenu.DropDownItems.Add(UiLanguage.T("Запустить"), null, (_, _) => start());
        scenarioMenu.DropDownItems.Add(UiLanguage.T("Пауза / продолжить"), null, (_, _) => togglePause());
        scenarioMenu.DropDownItems.Add(UiLanguage.T("Остановить"), null, (_, _) => stop());
        menu.Items.AddRange([fileMenu, scenarioMenu]);
        var settingsMenu = new ToolStripMenuItem(UiLanguage.T("Настройки"));
        settingsMenu.DropDownItems.Add(UiLanguage.T("Сценарий…"), null, (_, _) => settings());
        settingsMenu.DropDownItems.Add(UiLanguage.T("Назначения мыши…"), null, (_, _) => mouseSettings());
        menu.Items.Add(settingsMenu);
        var helpMenu = new ToolStripMenuItem(UiLanguage.T("Справка"));
        helpMenu.DropDownItems.Add(UiLanguage.T("Справка"), null, (_, _) => help());
        helpMenu.DropDownItems.Add(UiLanguage.T("Сайт и сообщество Kliker Blocks…"), null, (_, _) => WebsiteLinks.Open(this));
        helpMenu.DropDownItems.Add(UiLanguage.T("Поддержать проект"), null, (_, _) => new SupportForm().Show(this));
        helpMenu.DropDownItems.Add(UiLanguage.T("О программе…"), null, (_, _) => new AboutForm().Show(this));
        menu.Items.Add(helpMenu);

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(12, 7, 8, 0) };
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        details.Controls.Add(_scenario, 0, 0);
        details.Controls.Add(_state, 0, 1);
        details.Controls.Add(_window, 0, 2);
        root.Controls.Add(details, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6, 3, 0, 0) };
        var editor = new Button { Text = UiLanguage.T("✎ Редактор"), Width = 110, Height = 31, Margin = new Padding(2) };
        editor.Click += (_, _) => showEditor();
        _start.Click += (_, _) => start();
        _pause.Click += (_, _) => togglePause();
        _stop.Click += (_, _) => stop();
        buttons.Controls.AddRange([editor, _start, _pause, _stop]);
        root.Controls.Add(buttons, 0, 2);
        UpdateState("Без имени", false, false, false);
    }

    public void UpdateState(string scenarioName, bool running, bool paused, bool boundToWindow)
    {
        _scenario.Text = UiLanguage.T("Сценарий:") + " " + UiLanguage.T(scenarioName);
        _state.Text = UiLanguage.T(running ? paused ? "Состояние: ПАУЗА" : "Состояние: ВЫПОЛНЯЕТСЯ" : "Состояние: ГОТОВ");
        _window.Text = UiLanguage.T(boundToWindow ? "Привязка к окну: включена" : "Привязка к окну: выключена");
        _start.Enabled = !running;
        _pause.Enabled = running;
        _pause.Text = UiLanguage.T(paused ? "▶ Далее" : "⏸ Пауза");
        _stop.Enabled = running;
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}
