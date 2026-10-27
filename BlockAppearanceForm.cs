using System.Drawing.Drawing2D;

namespace KlikerBlocks;

internal sealed class BlockAppearanceForm : Form
{
    private readonly BlockNode _node;
    private readonly bool _hasInput;
    private readonly BlockAppearancePreview _preview;
    private readonly NumericUpDown _angle;
    private readonly ComboBox _port;
    private BlockAppearance _appearance;

    internal BlockAppearance Appearance => _appearance.Copy();
    internal bool SaveAsDefault { get; private set; }

    internal BlockAppearanceForm(BlockNode node)
    {
        _node = node;
        _hasInput = node.Kind != "Start";
        _appearance = BlockAppearanceManager.Resolve(node).Copy();
        Text = UiLanguage.T("Оформление блока") + " — " + UiLanguage.T(BlockCatalog.Get(node.Kind).Title);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 890;
        Height = 720;
        MinimumSize = new Size(720, 580);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(247, 250, 252);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            Padding = new Padding(18) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(root);
        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(3, 0, 14, 0) };
        root.Controls.Add(controls, 0, 0);
        void Label(string text) => controls.Controls.Add(new Label { Text = UiLanguage.T(text),
            Width = 300, Height = 26, Margin = new Padding(0, 10, 0, 2) });

        Label("Форма блока");
        var shapes = new (string Key, string Label)[] { ("Rounded", "Скруглённый прямоугольник"),
            ("Rectangle", "Прямоугольник"), ("Square", "Квадрат"), ("Circle", "Круг"),
            ("Diamond", "Ромб") };
        var shape = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        shape.Items.AddRange(shapes.Select(item => UiLanguage.T(item.Label)).Cast<object>().ToArray());
        shape.SelectedIndex = Math.Max(0, Array.FindIndex(shapes, item => item.Key == _appearance.Shape));
        controls.Controls.Add(shape);
        shape.SelectedIndexChanged += (_, _) =>
        {
            _appearance.Shape = shapes[shape.SelectedIndex].Key;
            SetSelectedAngle();
            _preview?.Invalidate();
        };

        var spec = BlockCatalog.Get(node.Kind);
        Label("Заливка блока");
        var fill = ColorButton(_appearance.FillColor.Length == 0 ? spec.Color :
            ColorTranslator.FromHtml(_appearance.FillColor), "Выбрать цвет заливки…");
        fill.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = _appearance.FillColor.Length == 0 ? spec.Color :
                ColorTranslator.FromHtml(_appearance.FillColor), FullOpen = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            _appearance.FillColor = Html(picker.Color);
            SetButtonColor(fill, picker.Color, "Выбрать цвет заливки…");
            _preview?.Invalidate();
        };
        controls.Controls.Add(fill);
        Label("Непрозрачность заливки, % (0–100)");
        var fillOpacity = Number(0, 100, _appearance.FillOpacityPercent, 5);
        fillOpacity.ValueChanged += (_, _) => { _appearance.FillOpacityPercent = (int)fillOpacity.Value; _preview?.Invalidate(); };
        controls.Controls.Add(fillOpacity);

        Label("Окантовка блока");
        var border = ColorButton(ColorTranslator.FromHtml(_appearance.BorderColor), "Выбрать цвет окантовки…");
        border.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = ColorTranslator.FromHtml(_appearance.BorderColor), FullOpen = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            _appearance.BorderColor = Html(picker.Color);
            SetButtonColor(border, picker.Color, "Выбрать цвет окантовки…");
            _preview?.Invalidate();
        };
        controls.Controls.Add(border);
        Label("Непрозрачность окантовки, % (0–100)");
        var borderOpacity = Number(0, 100, _appearance.BorderOpacityPercent, 5);
        borderOpacity.ValueChanged += (_, _) => { _appearance.BorderOpacityPercent = (int)borderOpacity.Value; _preview?.Invalidate(); };
        controls.Controls.Add(borderOpacity);
        Label("Толщина окантовки (1–12)");
        var borderWidth = Number(1, 12, _appearance.BorderWidth, .5m, 1);
        borderWidth.ValueChanged += (_, _) => { _appearance.BorderWidth = borderWidth.Value; _preview?.Invalidate(); };
        controls.Controls.Add(borderWidth);

        if (node.Kind != "Description") Label("Точка на контуре");
        _port = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        if (_hasInput) _port.Items.Add(UiLanguage.T("Вход"));
        foreach (var output in spec.Outputs) _port.Items.Add(UiLanguage.T("Выход") + ": " + UiLanguage.T(output));
        _port.SelectedIndex = 0;
        if (node.Kind != "Description") controls.Controls.Add(_port);
        if (node.Kind != "Description") Label("Угол точки (0–359°)");
        _angle = Number(0, 359, 0, 1);
        if (node.Kind != "Description") controls.Controls.Add(_angle);
        if (node.Kind != "Description") controls.Controls.Add(new Label { Text = UiLanguage.T("0° — справа, 90° — снизу, 180° — слева, 270° — сверху. Щёлкните по образцу, чтобы поставить выбранную точку на контур."),
            Width = 300, AutoSize = true, MaximumSize = new Size(300, 0),
            ForeColor = Color.FromArgb(76, 100, 119), Margin = new Padding(0, 7, 0, 12) });

        _preview = new BlockAppearancePreview(node, () => _appearance, () => _port.SelectedIndex)
        { Dock = DockStyle.Fill, BackColor = Color.White };
        _preview.AngleChosen += value => _angle.Value = value;
        root.Controls.Add(_preview, 1, 0);
        _port.SelectedIndexChanged += (_, _) => { SetSelectedAngle(); _preview.Invalidate(); };
        _angle.ValueChanged += (_, _) =>
        {
            if (_hasInput && _port.SelectedIndex == 0) _appearance.InputAngle = (int)_angle.Value;
            else _appearance.OutputAngles[spec.Outputs[_port.SelectedIndex - (_hasInput ? 1 : 0)]] = (int)_angle.Value;
            _preview.Invalidate();
        };
        SetSelectedAngle();

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Width = 100, Height = 36 };
        cancel.Click += (_, _) => Close();
        footer.Controls.Add(cancel);
        var saveLocal = new Button { Text = UiLanguage.T("Применить к блоку"), Width = 180, Height = 36 };
        saveLocal.Click += (_, _) => Finish(false);
        footer.Controls.Add(saveLocal);
        var saveDefault = new Button { Text = UiLanguage.T("Сделать стандартом для этого типа"), Width = 260, Height = 36 };
        saveDefault.Click += (_, _) => Finish(true);
        footer.Controls.Add(saveDefault);
        var standard = new Button { Text = UiLanguage.T("Стандартный вид"), Width = 150, Height = 36 };
        standard.Click += (_, _) =>
        {
            _appearance = new BlockAppearance();
            shape.SelectedIndex = 0;
            fillOpacity.Value = 100;
            borderOpacity.Value = 100;
            borderWidth.Value = 1;
            SetButtonColor(fill, spec.Color, "Выбрать цвет заливки…");
            SetButtonColor(border, ColorTranslator.FromHtml(_appearance.BorderColor), "Выбрать цвет окантовки…");
            SetSelectedAngle();
            _preview.Invalidate();
        };
        footer.Controls.Add(standard);
    }

    private void Finish(bool asDefault)
    {
        if (!_appearance.IsValid()) return;
        SaveAsDefault = asDefault;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetSelectedAngle()
    {
        var shape = _appearance.Shape;
        var height = shape is "Square" or "Circle" or "Diamond" ? BlockCatalog.Width :
            54 + 25 * Math.Max(1, BlockCatalog.Get(_node.Kind).Outputs.Length);
        var bounds = new Rectangle(0, 0, BlockCatalog.Width, height);
        var corner = shape is "Square" or "Circle" or "Diamond" ? 45 : BlockGeometry.DefaultCornerAngle(bounds);
        var outputIndex = _port.SelectedIndex - (_hasInput ? 1 : 0);
        var value = _hasInput && _port.SelectedIndex == 0 ? _appearance.InputAngle ?? 180 + corner :
            _appearance.OutputAngles.GetValueOrDefault(BlockCatalog.Get(_node.Kind).Outputs[outputIndex],
                (corner - outputIndex * 18 + 360) % 360);
        _angle.Value = value;
    }

    private static NumericUpDown Number(decimal min, decimal max, decimal value, decimal step, int decimals = 0) =>
        new() { Width = 300, Minimum = min, Maximum = max, Value = value,
            Increment = step, DecimalPlaces = decimals };

    private static string Html(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Button ColorButton(Color color, string caption)
    {
        var button = new Button { Width = 300, Height = 35 };
        SetButtonColor(button, color, caption);
        return button;
    }

    private static void SetButtonColor(Button button, Color color, string caption)
    {
        button.Text = UiLanguage.T(caption) + "  " + Html(color);
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() < .55f ? Color.White : Color.Black;
    }
}

internal sealed class BlockAppearancePreview(BlockNode node, Func<BlockAppearance> appearance,
    Func<int> selectedPort) : Control
{
    internal event Action<int>? AngleChosen;

    public BlockAppearancePreview() : this(new BlockNode(), () => new BlockAppearance(), () => 0) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var style = appearance();
        var bounds = PreviewBounds(style.Shape);
        var spec = BlockCatalog.Get(node.Kind);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BlockGeometry.Outline(bounds, style.Shape);
        var fillColor = style.FillColor.Length == 0 ? spec.Color : ColorTranslator.FromHtml(style.FillColor);
        using var fill = new SolidBrush(Color.FromArgb((int)Math.Round(255 * style.FillOpacityPercent / 100d), fillColor));
        using var border = new Pen(Color.FromArgb((int)Math.Round(255 * style.BorderOpacityPercent / 100d),
            ColorTranslator.FromHtml(style.BorderColor)), (float)style.BorderWidth);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        using var font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(UiLanguage.T(spec.Title), font, Brushes.White,
            new RectangleF(bounds.X + 40, bounds.Y + bounds.Height / 2f - 30, bounds.Width - 80, 60), format);
        var corner = style.Shape is "Square" or "Circle" or "Diamond" ? 45 : BlockGeometry.DefaultCornerAngle(bounds);
        if (node.Kind is not ("Description" or "Start"))
        {
            var input = BlockGeometry.OnContour(bounds, style.Shape, style.InputAngle ?? 180 + corner);
            DrawPort(e.Graphics, input, Color.FromArgb(46, 111, 229), selectedPort() == 0);
        }
        for (var index = 0; index < spec.Outputs.Length; index++)
        {
            var angle = style.OutputAngles.GetValueOrDefault(spec.Outputs[index],
                (corner - index * 18 + 360) % 360);
            var output = BlockGeometry.OnContour(bounds, style.Shape, angle);
            DrawPort(e.Graphics, output, Color.FromArgb(230, 71, 77),
                selectedPort() == index + (node.Kind == "Start" ? 0 : 1));
        }
        if (node.Kind != "Description")
        {
            using var instruction = new Font(Font.FontFamily, 9);
            e.Graphics.DrawString(UiLanguage.T("Щёлкните для перемещения выбранной точки"), instruction,
                Brushes.SlateGray, new RectangleF(12, Height - 48, Width - 24, 40), format);
        }
    }

    private static void DrawPort(Graphics graphics, Point point, Color color, bool selected)
    {
        using var fill = new SolidBrush(color);
        using var ring = new Pen(selected ? Color.Gold : Color.White, selected ? 3f : 2f);
        graphics.FillEllipse(fill, point.X - 8, point.Y - 8, 16, 16);
        graphics.DrawEllipse(ring, point.X - 8, point.Y - 8, 16, 16);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) Choose(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) Choose(e.Location);
    }

    private void Choose(Point point)
    {
        var bounds = PreviewBounds(appearance().Shape);
        var angle = (int)Math.Round(Math.Atan2(point.Y - (bounds.Top + bounds.Height / 2d),
            point.X - (bounds.Left + bounds.Width / 2d)) * 180 / Math.PI);
        AngleChosen?.Invoke((angle + 360) % 360);
    }

    private Rectangle PreviewBounds(string shape)
    {
        var width = Math.Min(BlockCatalog.Width, Math.Max(120, Width - 100));
        var height = shape is "Square" or "Circle" or "Diamond" ? width :
            (int)Math.Round(width / (double)BlockCatalog.Width *
                (54 + 25 * Math.Max(1, BlockCatalog.Get(node.Kind).Outputs.Length)));
        return new Rectangle((Width - width) / 2, Math.Max(65, (Height - height) / 2 - 18), width, height);
    }
}
