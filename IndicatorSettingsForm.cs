namespace KlikerBlocks;

internal sealed class IndicatorSettingsForm : Form
{
    private readonly RadioButton _automatic = new() { Text = "Автоматически: справа вверху монитора", AutoSize = true };
    private readonly RadioButton _fixed = new() { Text = "Закрепить по экранным координатам", AutoSize = true };
    private readonly NumericUpDown _x = new() { Minimum = -100000, Maximum = 100000, Width = 110 };
    private readonly NumericUpDown _y = new() { Minimum = -100000, Maximum = 100000, Width = 110 };

    public bool UseCustomPosition => _fixed.Checked;
    public int IndicatorX => (int)_x.Value;
    public int IndicatorY => (int)_y.Value;

    public IndicatorSettingsForm(ProjectModel settings, Point? currentPosition)
    {
        Text = "Положение индикатора";
        Width = 560;
        Height = 310;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
        var size = (settings.IndicatorAppearance ?? new IndicatorAppearance()).EffectiveSize();
        var width = size.Width;
        var height = size.Height;
        var fallback = currentPosition ?? new Point(workingArea.Right - width - 18, workingArea.Top + 18);
        _x.Value = Math.Clamp(settings.UseCustomIndicatorPosition ? settings.IndicatorX : fallback.X,
            (int)_x.Minimum, (int)_x.Maximum);
        _y.Value = Math.Clamp(settings.UseCustomIndicatorPosition ? settings.IndicatorY : fallback.Y,
            (int)_y.Minimum, (int)_y.Maximum);
        _fixed.Checked = settings.UseCustomIndicatorPosition;
        _automatic.Checked = !_fixed.Checked;

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(panel);
        panel.Controls.Add(new Label
        {
            Text = "Координаты указывают левый верхний угол индикатора.",
            AutoSize = false, Dock = DockStyle.Fill
        }, 0, 0);
        panel.Controls.Add(_automatic, 0, 1);
        panel.Controls.Add(_fixed, 0, 2);
        var coordinates = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        coordinates.Controls.Add(new Label { Text = "X", AutoSize = true, Margin = new Padding(0, 7, 5, 0) });
        coordinates.Controls.Add(_x);
        coordinates.Controls.Add(new Label { Text = "Y", AutoSize = true, Margin = new Padding(18, 7, 5, 0) });
        coordinates.Controls.Add(_y);
        panel.Controls.Add(coordinates, 0, 3);
        void RefreshInputs() { _x.Enabled = _fixed.Checked; _y.Enabled = _fixed.Checked; }
        _automatic.CheckedChanged += (_, _) => RefreshInputs();
        _fixed.CheckedChanged += (_, _) => RefreshInputs();
        RefreshInputs();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var apply = new Button { Text = "Применить", Width = 105, Height = 34 };
        var cancel = new Button { Text = "Отмена", Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
        apply.Click += (_, _) =>
        {
            if (_fixed.Checked && !Screen.AllScreens.Any(screen =>
                    screen.Bounds.Contains(new Rectangle(IndicatorX, IndicatorY, width, height))))
            {
                MessageBox.Show(this, $"Индикатор {width}×{height} пикселей должен целиком находиться на одном из экранов.",
                    "Координаты вне экрана", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        panel.Controls.Add(buttons, 0, 4);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
