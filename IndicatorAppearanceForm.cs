namespace KlikerBlocks;

internal sealed class IndicatorAppearanceForm : Form
{
    private readonly NumericUpDown _width = new() { Minimum = 24, Maximum = 800, Width = 100 };
    private readonly NumericUpDown _height = new() { Minimum = 24, Maximum = 300, Width = 100 };
    private readonly NumericUpDown _symbolSize = new() { Minimum = 6, Maximum = 160, Width = 100 };
    private readonly NumericUpDown _backgroundOpacity = new() { Minimum = 0, Maximum = 100, Width = 100 };
    private readonly NumericUpDown _runningOpacity = new() { Minimum = 0, Maximum = 100, Width = 100 };
    private readonly NumericUpDown _activeOpacity = new() { Minimum = 0, Maximum = 100, Width = 100 };
    private readonly NumericUpDown _inactiveOpacity = new() { Minimum = 0, Maximum = 100, Width = 100 };
    private readonly ComboBox _windowShape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _symbol = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _runningTitleVisible = new() { Text = "Показывать заголовок при запуске", AutoSize = true };
    private readonly CheckBox _activeTitleVisible = new() { Text = "Показывать заголовок при включённом слежении", AutoSize = true };
    private readonly CheckBox _inactiveTitleVisible = new() { Text = "Показывать заголовок при выключенном слежении", AutoSize = true };
    private readonly CheckBox _activationVisible = new() { Text = "Показывать F9: включено / выключено (или назначенную клавишу)", AutoSize = true };
    private readonly TextBox _runningTitle = new() { Width = 450 };
    private readonly TextBox _activeTitle = new() { Width = 450 };
    private readonly TextBox _inactiveTitle = new() { Width = 450 };
    private readonly TextBox _hotkey = new() { Width = 250 };
    private readonly string? _projectDirectory;
    private readonly IndicatorVisualChoice _backgroundVisual;
    private readonly IndicatorVisualChoice _runningVisual;
    private readonly IndicatorVisualChoice _activeVisual;
    private readonly IndicatorVisualChoice _inactiveVisual;

    public IndicatorAppearance Appearance { get; private set; }
    public string ToggleHotkey => _hotkey.Text.Trim();

    public IndicatorAppearanceForm(IndicatorAppearance? current, string hotkey, string? projectDirectory)
    {
        Appearance = (current ?? new IndicatorAppearance()).Copy();
        _projectDirectory = projectDirectory;
        if (!Appearance.ShowTitle)
            Appearance.ShowRunningTitle = Appearance.ShowActiveTitle = Appearance.ShowInactiveTitle = false;
        if (Appearance.Symbol == "Изображение" && !string.IsNullOrWhiteSpace(Appearance.ImagePath))
        {
            Appearance.Symbol = "Круг";
            MigrateLegacyImage();
        }

        Text = "Оформление индикатора сценария";
        Width = 720;
        Height = Math.Min(650, Math.Max(470, Screen.FromPoint(Cursor.Position).WorkingArea.Height - 60));
        MinimumSize = new Size(610, 470);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(layout);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        layout.Controls.Add(tabs, 0, 0);
        var shapeTab = new TabPage("Форма и фон");
        var statesTab = new TabPage("Состояния");
        var labelsTab = new TabPage("Надписи и клавиша");
        tabs.TabPages.AddRange([shapeTab, statesTab, labelsTab]);
        var shapeContent = CreateContent(shapeTab);
        var statesContent = CreateContent(statesTab);
        var labelsContent = CreateContent(labelsTab);

        AddSection(shapeContent, "Форма всего окна — края за пределами фигуры прозрачны");
        _windowShape.Items.AddRange(ScenarioIndicatorForm.WindowShapes);
        _windowShape.SelectedItem = _windowShape.Items.Contains(Appearance.WindowShape)
            ? Appearance.WindowShape : "Скруглённый прямоугольник";
        AddField(shapeContent, "Форма окна", _windowShape);
        _width.Value = Math.Clamp(Appearance.Width, 24, 800);
        _height.Value = Math.Clamp(Appearance.Height, 24, 300);
        AddField(shapeContent, "Ширина окна, пиксели", _width);
        AddField(shapeContent, "Высота окна, пиксели", _height);
        shapeContent.Controls.Add(new Label { Text = "Для круга и квадратных форм используется меньшая из двух сторон.", Width = 620, Height = 26 });
        _symbol.Items.AddRange(["Круг", "Квадрат", "Ромб"]);
        _symbol.SelectedItem = _symbol.Items.Contains(Appearance.Symbol) ? Appearance.Symbol : "Круг";
        _symbolSize.Value = Math.Clamp(Appearance.SymbolSize, 6, 160);
        AddField(shapeContent, "Значок внутри окна", _symbol);
        AddField(shapeContent, "Размер значка, пиксели", _symbolSize);
        shapeContent.Controls.Add(new Label { Text = "Если значок достигает края окна, окно принимает размер и форму значка; фон и надписи скрываются.", Width = 620, Height = 44 });
        AddSection(shapeContent, "Фон окна");
        _backgroundVisual = new IndicatorVisualChoice("Фон: цвет или картинка на всё окно", Appearance.BackgroundMode,
            Appearance.BackgroundColor, Appearance.BackgroundImagePath);
        shapeContent.Controls.Add(_backgroundVisual);
        _backgroundOpacity.Value = Math.Clamp(Appearance.BackgroundOpacityPercent, 0, 100);
        AddField(shapeContent, "Непрозрачность фона, % (0 = полностью прозрачно)", _backgroundOpacity);

        AddSection(statesContent, "Вид значка для каждого состояния");
        statesContent.Controls.Add(new Label { Text = "Картинка заменяет цветной значок. Если поле картинки пустое, показывается выбранный цвет.", Width = 620, Height = 42 });
        _runningVisual = new IndicatorVisualChoice("Сценарий запущен", Appearance.RunningMode,
            Appearance.RunningColor, Appearance.RunningImagePath);
        _activeVisual = new IndicatorVisualChoice("Активно: слежение включено или условия клавиш выполнены", Appearance.ActiveMode,
            Appearance.ActiveColor, Appearance.ActiveImagePath);
        _inactiveVisual = new IndicatorVisualChoice("Ожидание: слежение выключено или клавиши не нажаты", Appearance.InactiveMode,
            Appearance.InactiveColor, Appearance.InactiveImagePath);
        statesContent.Controls.Add(_runningVisual);
        _runningOpacity.Value = Math.Clamp(Appearance.RunningOpacityPercent, 0, 100);
        AddField(statesContent, "Непрозрачность значка «запущен», %", _runningOpacity);
        statesContent.Controls.Add(_activeVisual);
        _activeOpacity.Value = Math.Clamp(Appearance.ActiveOpacityPercent, 0, 100);
        AddField(statesContent, "Непрозрачность значка «активно», %", _activeOpacity);
        statesContent.Controls.Add(_inactiveVisual);
        _inactiveOpacity.Value = Math.Clamp(Appearance.InactiveOpacityPercent, 0, 100);
        AddField(statesContent, "Непрозрачность значка «ожидание», %", _inactiveOpacity);

        AddSection(labelsContent, "Заголовки — каждый включается отдельно");
        _runningTitleVisible.Checked = Appearance.ShowRunningTitle;
        _activeTitleVisible.Checked = Appearance.ShowActiveTitle;
        _inactiveTitleVisible.Checked = Appearance.ShowInactiveTitle;
        _activationVisible.Checked = Appearance.ShowDetail;
        _runningTitle.Text = Appearance.RunningTitle;
        _activeTitle.Text = Appearance.ActiveTitle;
        _inactiveTitle.Text = Appearance.InactiveTitle;
        AddTitleField(labelsContent, _runningTitleVisible, _runningTitle);
        AddTitleField(labelsContent, _activeTitleVisible, _activeTitle);
        AddTitleField(labelsContent, _inactiveTitleVisible, _inactiveTitle);
        labelsContent.Controls.Add(_activationVisible);
        labelsContent.Controls.Add(new Label { Text = "Цвет «слежение активно» включается сразу после F9. Нажатие ЛКМ цвет не меняет.", Width = 620, Height = 42 });
        AddSection(labelsContent, "Показать или скрыть окно во время работы");
        _hotkey.Text = hotkey;
        AddField(labelsContent, "Глобальная клавиша (например Ctrl+Shift+I)", _hotkey);
        labelsContent.Controls.Add(new Label { Text = "У двух экземпляров задайте разные комбинации: Windows отдаёт глобальную клавишу только одному окну.", Width = 620, Height = 42 });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(12), BackColor = SystemColors.Control };
        var save = new Button { Text = "Применить", Width = 110, Height = 34 };
        save.Click += (_, _) => SaveValues();
        var cancel = new Button { Text = "Отмена", Width = 95, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 1);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void MigrateLegacyImage()
    {
        if (Appearance.RunningMode == "Цвет" && Appearance.RunningImagePath.Length == 0)
        { Appearance.RunningMode = "Картинка"; Appearance.RunningImagePath = Appearance.ImagePath; }
        if (Appearance.ActiveMode == "Цвет" && Appearance.ActiveImagePath.Length == 0)
        { Appearance.ActiveMode = "Картинка"; Appearance.ActiveImagePath = Appearance.ImagePath; }
        if (Appearance.InactiveMode == "Цвет" && Appearance.InactiveImagePath.Length == 0)
        { Appearance.InactiveMode = "Картинка"; Appearance.InactiveImagePath = Appearance.ImagePath; }
    }

    private static FlowLayoutPanel CreateContent(TabPage page)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(16), AutoScrollMargin = new Size(0, 12) };
        page.Controls.Add(panel);
        return panel;
    }

    private static void AddSection(FlowLayoutPanel panel, string text) => panel.Controls.Add(new Label
    {
        Text = text, Width = 620, Height = 30, Font = new Font(panel.Font, FontStyle.Bold), Margin = new Padding(0, 10, 0, 3)
    });

    private static void AddField(FlowLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, Width = 620, Height = 24, Margin = new Padding(0, 5, 0, 0) });
        panel.Controls.Add(control);
    }

    private static void AddTitleField(FlowLayoutPanel panel, CheckBox checkbox, TextBox text)
    {
        panel.Controls.Add(checkbox);
        panel.Controls.Add(text);
        text.Enabled = checkbox.Checked;
        checkbox.CheckedChanged += (_, _) => text.Enabled = checkbox.Checked;
    }

    private void SaveValues()
    {
        try
        {
            var hotkey = HotkeyParser.Parse(_hotkey.Text);
            if (hotkey == HotkeyParser.Parse("Ctrl+Alt+F6") || hotkey == HotkeyParser.Parse("Ctrl+Alt+F7") ||
                hotkey == HotkeyParser.Parse("Ctrl+Alt+F10"))
                throw new ArgumentException("Эта комбинация зарезервирована для запуска или остановки.");
            foreach (var choice in new[] { _backgroundVisual, _runningVisual, _activeVisual, _inactiveVisual })
                choice.ValidateImage(_projectDirectory);
            Appearance = new IndicatorAppearance
            {
                Width = (int)_width.Value, Height = (int)_height.Value, WindowShape = _windowShape.SelectedItem?.ToString() ?? "Скруглённый прямоугольник",
                SymbolSize = (int)_symbolSize.Value, Symbol = _symbol.SelectedItem?.ToString() ?? "Круг",
                ShowTitle = _runningTitleVisible.Checked || _activeTitleVisible.Checked || _inactiveTitleVisible.Checked,
                ShowRunningTitle = _runningTitleVisible.Checked, ShowActiveTitle = _activeTitleVisible.Checked,
                ShowInactiveTitle = _inactiveTitleVisible.Checked, ShowDetail = _activationVisible.Checked,
                RunningTitle = _runningTitle.Text, ActiveTitle = _activeTitle.Text, InactiveTitle = _inactiveTitle.Text,
                BackgroundMode = _backgroundVisual.Mode, BackgroundColor = _backgroundVisual.HexColor,
                BackgroundOpacityPercent = (int)_backgroundOpacity.Value,
                BackgroundImagePath = _backgroundVisual.ImagePath, RunningMode = _runningVisual.Mode,
                RunningColor = _runningVisual.HexColor, RunningImagePath = _runningVisual.ImagePath,
                RunningOpacityPercent = (int)_runningOpacity.Value,
                ActiveMode = _activeVisual.Mode, ActiveColor = _activeVisual.HexColor, ActiveImagePath = _activeVisual.ImagePath,
                ActiveOpacityPercent = (int)_activeOpacity.Value,
                InactiveMode = _inactiveVisual.Mode, InactiveColor = _inactiveVisual.HexColor, InactiveImagePath = _inactiveVisual.ImagePath,
                InactiveOpacityPercent = (int)_inactiveOpacity.Value,
                ImagePath = ""
            };
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Проверьте настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}

internal sealed class IndicatorVisualChoice : GroupBox
{
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _color = new() { Width = 120, Height = 32, UseVisualStyleBackColor = false };
    private readonly TextBox _imagePath = new() { Dock = DockStyle.Fill };
    private readonly Button _browse = new() { Text = "Обзор…", Dock = DockStyle.Fill };

    public string Mode => _mode.SelectedItem?.ToString() ?? "Цвет";
    public string HexColor => _color.Text;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ImagePath { get => _imagePath.Text.Trim(); set => _imagePath.Text = value; }

    public IndicatorVisualChoice(string title, string mode, string color, string imagePath)
    {
        Text = title;
        Width = 630;
        Height = 112;
        Margin = new Padding(0, 5, 0, 8);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(9, 4, 9, 7), ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(layout);
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _mode.Items.AddRange(["Цвет", "Картинка"]);
        _mode.SelectedItem = mode == "Картинка" ? "Картинка" : "Цвет";
        _color.Text = color;
        _color.BackColor = IndicatorAppearance.ParseColor(color, System.Drawing.Color.Gray);
        _color.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _color.BackColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _color.BackColor = dialog.Color;
            _color.Text = ColorTranslator.ToHtml(dialog.Color);
        };
        top.Controls.Add(_mode);
        top.Controls.Add(_color);
        layout.Controls.Add(top, 0, 0);
        _imagePath.Text = imagePath;
        _imagePath.PlaceholderText = "Путь к PNG/JPG/BMP; пусто = цвет";
        layout.Controls.Add(_imagePath, 0, 1);
        layout.Controls.Add(_browse, 1, 1);
        _browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _imagePath.Text = dialog.FileName;
            _mode.SelectedItem = "Картинка";
        };
        _mode.SelectedIndexChanged += (_, _) => RefreshMode();
        RefreshMode();
    }

    private void RefreshMode()
    {
        _color.Enabled = Mode == "Цвет";
        _imagePath.Enabled = Mode == "Картинка";
        _browse.Enabled = true;
    }

    public void ValidateImage(string? projectDirectory)
    {
        if (Mode != "Картинка" || ImagePath.Length == 0) return;
        var fullPath = Path.IsPathRooted(ImagePath) ? ImagePath : Path.Combine(projectDirectory ?? AppContext.BaseDirectory, ImagePath);
        if (!File.Exists(fullPath)) throw new ArgumentException($"Картинка «{Text}» не найдена. Очистите её поле или выберите существующий файл.");
        using var _ = Image.FromFile(fullPath);
    }
}
