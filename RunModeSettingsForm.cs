namespace KlikerBlocks;

internal sealed class RunModeSettingsForm : Form
{
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _delay = new() { Minimum = 10, Maximum = 60000, Increment = 10, Width = 120 };
    private readonly NumericUpDown _speed = new() { Minimum = 0.1m, Maximum = 5.0m,
        Increment = 0.1m, DecimalPlaces = 1, Width = 120 };

    public string RunMode => _mode.SelectedIndex == 1 ? "Повторять до остановки" : "Один проход";
    public int CycleDelayMs => (int)_delay.Value;
    public decimal PlaybackSpeed => _speed.Value;

    public RunModeSettingsForm(ProjectModel project)
    {
        Text = UiLanguage.T("Режим выполнения сценария");
        Width = 590;
        Height = 415;
        MinimumSize = new Size(530, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(root);
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
        root.Controls.Add(content, 0, 0);
        content.Controls.Add(new Label { Text = UiLanguage.T("Как продолжать после конца цепочки блоков"), Width = 520, Height = 27,
            Font = new Font(Font, FontStyle.Bold) });
        _mode.Items.AddRange([UiLanguage.T("Один проход"), UiLanguage.T("Повторять до остановки")]);
        _mode.SelectedIndex = project.RunMode == "Повторять до остановки" ? 1 : 0;
        content.Controls.Add(_mode);
        content.Controls.Add(new Label { Text = UiLanguage.T("Пауза между проходами и при возврате по обратной связи, мс"), Width = 520,
            Height = 28, Margin = new Padding(0, 13, 0, 0) });
        _delay.Value = Math.Clamp(project.CycleDelayMs, 10, 60000);
        content.Controls.Add(_delay);
        content.Controls.Add(new Label { Text = UiLanguage.T("Скорость выполнения (0,1–5,0×)"), Width = 520,
            Height = 28, Margin = new Padding(0, 13, 0, 0) });
        _speed.Value = Math.Clamp(project.PlaybackSpeed, 0.1m, 5.0m);
        content.Controls.Add(_speed);
        content.Controls.Add(new Label
        {
            Text = UiLanguage.T("«Повторять до остановки» снова начинает со «Старт», если путь закончился. Можно также соединить последний блок с предыдущим: явная обратная связь автоматически повторяет сценарий даже в режиме одного прохода. Блок «Стоп» и меню остановки завершают его."),
            Width = 520, Height = 120, Margin = new Padding(0, 14, 0, 0)
        });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(12) };
        var apply = new Button { Text = UiLanguage.T("Применить"), Width = 110, Height = 34 };
        apply.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Width = 95, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 1);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
