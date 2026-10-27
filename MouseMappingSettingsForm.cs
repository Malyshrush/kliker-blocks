using System.Text.Json;

namespace KlikerBlocks;

internal sealed class MouseMappingSettingsForm : Form
{
    private readonly MouseMappingSettings _settings;
    private readonly CheckBox _enabled = new() { Text = "Включить переназначение кнопок мыши для этого проекта", AutoSize = true };
    private readonly ListBox _profiles = new() { Dock = DockStyle.Fill };
    private readonly TextBox _name = new() { Width = 170 };
    private readonly ComboBox _match = new() { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _process = new() { Width = 150 };
    private readonly TextBox _title = new() { Width = 190 };
    private readonly TextBox _class = new() { Width = 145 };
    private readonly CheckBox _region = new() { Text = "Область внутри окна", AutoSize = true };
    private readonly NumericUpDown _regionX = Number(-10000, 10000, 0);
    private readonly NumericUpDown _regionY = Number(-10000, 10000, 0);
    private readonly NumericUpDown _regionW = Number(1, 10000, 200);
    private readonly NumericUpDown _regionH = Number(1, 10000, 200);
    private readonly ComboBox _layers = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _bindings = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells };
    private bool _loading;
    private int _selectedProfile = -1;
    private int _selectedLayer = -1;
    public MouseMappingSettings Result => _settings;

    public MouseMappingSettingsForm(MouseMappingSettings current)
    {
        _settings = JsonSerializer.Deserialize<MouseMappingSettings>(JsonSerializer.Serialize(current)) ?? new();
        Text = "Профили и назначения мыши";
        Width = 1250; Height = 760; MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);
        _enabled.Checked = _settings.Enabled;
        root.Controls.Add(_enabled, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 245, FixedPanel = FixedPanel.Panel1 };
        root.Controls.Add(split, 0, 1);
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        split.Panel1.Controls.Add(left);
        left.Controls.Add(new Label { Text = "Профили (первый подходящий используется)", Dock = DockStyle.Fill }, 0, 0);
        left.Controls.Add(_profiles, 0, 1);
        var profileButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        left.Controls.Add(profileButtons, 0, 2);
        AddButton(profileButtons, "+ Профиль", 104, AddProfile);
        AddButton(profileButtons, "Удалить", 104, RemoveProfile);
        AddButton(profileButtons, "↑", 48, () => MoveProfile(-1));
        AddButton(profileButtons, "↓", 48, () => MoveProfile(1));

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 187));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        split.Panel2.Controls.Add(right);
        var profileFields = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        right.Controls.Add(profileFields, 0, 0);
        AddField(profileFields, "Имя", _name);
        _match.Items.AddRange(["Активное окно", "Под курсором", "Все окна"]);
        AddField(profileFields, "Активация", _match);
        AddField(profileFields, "Имя процесса (.exe)", _process);
        AddField(profileFields, "Заголовок содержит", _title);
        AddField(profileFields, "Класс элемента/окна", _class);
        profileFields.Controls.Add(_region);
        AddField(profileFields, "X", _regionX); AddField(profileFields, "Y", _regionY);
        AddField(profileFields, "Ширина", _regionW); AddField(profileFields, "Высота", _regionH);
        var layerBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        right.Controls.Add(layerBar, 0, 1);
        layerBar.Controls.Add(new Label { Text = "Слой:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        layerBar.Controls.Add(_layers);
        AddButton(layerBar, "+ Слой", 77, AddLayer);
        AddButton(layerBar, "Удалить слой", 110, RemoveLayer);
        AddButton(layerBar, "Переименовать", 110, RenameLayer);
        SetupGrid();
        right.Controls.Add(_bindings, 0, 2);
        var bindingsBar = new FlowLayoutPanel { Dock = DockStyle.Fill };
        right.Controls.Add(bindingsBar, 0, 3);
        AddButton(bindingsBar, "+ Назначение", 130, () => _bindings.Rows.Add("Кнопка 4", "Нажатие", "", 300, "Клавиши", "Ctrl+C", true));
        AddButton(bindingsBar, "Копировать", 105, CopyBinding);
        AddButton(bindingsBar, "Удалить", 90, () => { if (_bindings.CurrentRow is { } row) _bindings.Rows.Remove(row); });

        root.Controls.Add(new Label { Dock = DockStyle.Fill, AutoEllipsis = true,
            Text = "Клавиши: Ctrl+C, 100ms, Ctrl+V. Для сочетания укажите вторую удерживаемую кнопку. Короткое нажатие срабатывает при отпускании, если не было долгого. Долгих порогов на кнопку — до четырёх. Колесо поддерживает нажатие без удержания." }, 0, 2);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        root.Controls.Add(bottom, 0, 3);
        AddButton(bottom, "Применить", 110, Save);
        AddButton(bottom, "Отмена", 100, () => { DialogResult = DialogResult.Cancel; Close(); });
        _profiles.SelectedIndexChanged += (_, _) => SelectProfile();
        _name.TextChanged += (_, _) =>
        {
            if (!_loading && _selectedProfile >= 0) _profiles.Items[_selectedProfile] = _name.Text.Trim();
        };
        _layers.SelectedIndexChanged += (_, _) => SelectLayer();
        foreach (var control in new Control[] { _name, _match, _process, _title, _class, _region,
            _regionX, _regionY, _regionW, _regionH })
        {
            if (control is TextBox box) Attach(box);
            else if (control is ComboBox combo) Attach(combo);
            else if (control is CheckBox check) Attach(check);
            else if (control is NumericUpDown number) Attach(number);
        }
        RefreshProfiles();
        if (_settings.Profiles.Count > 0) _profiles.SelectedIndex = 0;
    }

    private void Attach(TextBox box) => box.TextChanged += (_, _) => WriteProfile();
    private void Attach(ComboBox box) => box.SelectedIndexChanged += (_, _) => WriteProfile();
    private void Attach(CheckBox box) => box.CheckedChanged += (_, _) => WriteProfile();
    private void Attach(NumericUpDown box) => box.ValueChanged += (_, _) => WriteProfile();
    private static NumericUpDown Number(int min, int max, int value) => new()
        { Minimum = min, Maximum = max, Value = value, Width = 80 };
    private static void AddField(FlowLayoutPanel panel, string label, Control control)
    {
        var row = new FlowLayoutPanel { Width = Math.Max(170, control.Width + 15), Height = 51,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(3) };
        row.Controls.Add(new Label { Text = label, Width = row.Width - 8, Height = 19 });
        row.Controls.Add(control);
        panel.Controls.Add(row);
    }
    private static void AddButton(FlowLayoutPanel row, string text, int width, Action action)
    {
        var button = new Button { Text = text, Width = width, Height = 30 };
        button.Click += (_, _) => action();
        row.Controls.Add(button);
    }

    private void SetupGrid()
    {
        _bindings.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Кнопка/колесо", Width = 115,
            DataSource = MouseMappingNames.Sources });
        _bindings.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Когда", Width = 145,
            DataSource = MouseMappingNames.Triggers });
        _bindings.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Удерживать вместе с", Width = 135,
            DataSource = new[] { "" }.Concat(MouseMappingNames.Sources.Where(source => !MouseMappingNames.IsWheel(source))).ToArray() });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Порог/повтор, мс", Width = 120 });
        _bindings.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Действие", Width = 170,
            DataSource = MouseMappingNames.Actions });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Значение", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _bindings.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Блокировать исходное", Width = 105 });
        _bindings.DataError += (_, e) => { e.ThrowException = false; };
    }

    private void RefreshProfiles()
    {
        _loading = true;
        _profiles.Items.Clear();
        foreach (var profile in _settings.Profiles) _profiles.Items.Add(profile.Name);
        _loading = false;
    }

    private void SelectProfile()
    {
        if (_loading) return;
        SaveBindings();
        _selectedProfile = _profiles.SelectedIndex;
        _selectedLayer = -1;
        _loading = true;
        _bindings.Rows.Clear(); _layers.Items.Clear();
        if (_selectedProfile >= 0)
        {
            var profile = _settings.Profiles[_selectedProfile];
            _name.Text = profile.Name; _match.SelectedItem = profile.MatchMode;
            _process.Text = profile.ProcessName; _title.Text = profile.TitleContains;
            _class.Text = profile.WindowClass; _region.Checked = profile.UseRegion;
            _regionX.Value = Math.Clamp(profile.RegionX, (int)_regionX.Minimum, (int)_regionX.Maximum);
            _regionY.Value = Math.Clamp(profile.RegionY, (int)_regionY.Minimum, (int)_regionY.Maximum);
            _regionW.Value = Math.Clamp(profile.RegionWidth, 1, (int)_regionW.Maximum);
            _regionH.Value = Math.Clamp(profile.RegionHeight, 1, (int)_regionH.Maximum);
            foreach (var layer in profile.Layers) _layers.Items.Add(layer.Name);
        }
        _loading = false;
        if (_layers.Items.Count > 0) _layers.SelectedIndex = 0;
    }

    private void SelectLayer()
    {
        if (_loading) return;
        SaveBindings();
        _selectedLayer = _layers.SelectedIndex;
        _bindings.Rows.Clear();
        if (_selectedProfile < 0 || _selectedLayer < 0) return;
        foreach (var binding in _settings.Profiles[_selectedProfile].Layers[_selectedLayer].Bindings)
        {
            var row = _bindings.Rows[_bindings.Rows.Add(binding.Source, binding.Trigger, binding.ChordSource,
                binding.DelayMs, binding.Action, binding.Value, binding.BlockOriginal)];
            row.Tag = binding.Id;
        }
    }

    private void WriteProfile()
    {
        if (_loading || _selectedProfile < 0) return;
        var profile = _settings.Profiles[_selectedProfile];
        profile.Name = _name.Text.Trim(); profile.MatchMode = _match.Text;
        profile.ProcessName = _process.Text.Trim(); profile.TitleContains = _title.Text.Trim();
        profile.WindowClass = _class.Text.Trim(); profile.UseRegion = _region.Checked;
        profile.RegionX = (int)_regionX.Value; profile.RegionY = (int)_regionY.Value;
        profile.RegionWidth = (int)_regionW.Value; profile.RegionHeight = (int)_regionH.Value;
    }

    private void SaveBindings()
    {
        if (_selectedProfile < 0 || _selectedLayer < 0) return;
        _bindings.EndEdit();
        var saved = new List<MouseMappingBinding>();
        foreach (DataGridViewRow row in _bindings.Rows)
        {
            var old = _settings.Profiles[_selectedProfile].Layers[_selectedLayer].Bindings
                .FirstOrDefault(binding => binding.Id == (row.Tag is Guid id ? id : Guid.Empty));
            saved.Add(new MouseMappingBinding
            {
                Id = old?.Id ?? Guid.NewGuid(),
                Source = Convert.ToString(row.Cells[0].Value) ?? "",
                Trigger = Convert.ToString(row.Cells[1].Value) ?? "",
                ChordSource = Convert.ToString(row.Cells[2].Value) ?? "",
                DelayMs = int.TryParse(Convert.ToString(row.Cells[3].Value), out var delay) ? delay : -1,
                Action = Convert.ToString(row.Cells[4].Value) ?? "",
                Value = Convert.ToString(row.Cells[5].Value) ?? "",
                BlockOriginal = row.Cells[6].Value is true
            });
        }
        _settings.Profiles[_selectedProfile].Layers[_selectedLayer].Bindings = saved;
    }

    private void AddProfile()
    {
        SaveBindings();
        _settings.Profiles.Add(new MouseMappingProfile { Name = $"Профиль {_settings.Profiles.Count + 1}" });
        RefreshProfiles(); _profiles.SelectedIndex = _settings.Profiles.Count - 1;
    }
    private void RemoveProfile()
    {
        var index = _profiles.SelectedIndex;
        if (index < 0) return;
        _selectedProfile = -1; _selectedLayer = -1;
        _settings.Profiles.RemoveAt(index);
        RefreshProfiles();
        if (_settings.Profiles.Count > 0) _profiles.SelectedIndex = Math.Min(index, _settings.Profiles.Count - 1);
        else { _layers.Items.Clear(); _bindings.Rows.Clear(); }
    }
    private void MoveProfile(int delta)
    {
        SaveBindings();
        var index = _profiles.SelectedIndex;
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _settings.Profiles.Count) return;
        (_settings.Profiles[index], _settings.Profiles[target]) = (_settings.Profiles[target], _settings.Profiles[index]);
        _selectedProfile = -1; _selectedLayer = -1;
        RefreshProfiles(); _profiles.SelectedIndex = target;
    }
    private void AddLayer()
    {
        if (_selectedProfile < 0) return;
        var layers = _settings.Profiles[_selectedProfile].Layers;
        if (layers.Count >= 10) { MessageBox.Show(this, "Максимум 10 слоёв."); return; }
        SaveBindings();
        layers.Add(new MouseMappingLayer { Name = $"Слой {layers.Count + 1}" });
        _layers.Items.Add(layers[^1].Name); _layers.SelectedIndex = layers.Count - 1;
    }
    private void RemoveLayer()
    {
        if (_selectedProfile < 0 || _selectedLayer < 0) return;
        var layers = _settings.Profiles[_selectedProfile].Layers;
        if (layers.Count == 1) { MessageBox.Show(this, "Должен остаться хотя бы один слой."); return; }
        var index = _selectedLayer;
        _selectedLayer = -1;
        layers.RemoveAt(index); _layers.Items.RemoveAt(index);
        _layers.SelectedIndex = Math.Min(index, layers.Count - 1);
    }
    private void RenameLayer()
    {
        if (_selectedProfile < 0 || _selectedLayer < 0) return;
        var layer = _settings.Profiles[_selectedProfile].Layers[_selectedLayer];
        var text = Microsoft.VisualBasic.Interaction.InputBox("Новое имя слоя:", "Слой", layer.Name).Trim();
        if (text.Length == 0) return;
        layer.Name = text;
        _layers.Items[_selectedLayer] = text;
    }
    private void CopyBinding()
    {
        if (_bindings.CurrentRow is not { } row) return;
        var values = new object[7];
        for (var i = 0; i < values.Length; i++) values[i] = row.Cells[i].Value ?? (i == 6 ? false : "");
        var index = _bindings.Rows.Add(values);
        _bindings.Rows[index].Tag = null;
    }
    private void Save()
    {
        SaveBindings();
        _settings.Enabled = _enabled.Checked;
        var error = MouseMappingValidator.Check(_settings);
        if (error is not null) { MessageBox.Show(this, error, "Проверьте назначения", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal static class MouseMappingValidator
{
    public static string? Check(MouseMappingSettings settings)
    {
        if (settings.Enabled && settings.Profiles.Count == 0) return "Добавьте хотя бы один профиль или выключите назначения.";
        if (settings.Profiles.Count > 100) return "Слишком много профилей (максимум 100).";
        foreach (var profile in settings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name)) return "У каждого профиля должно быть имя.";
            if (profile.MatchMode is not ("Активное окно" or "Под курсором" or "Все окна")) return $"Неверный режим профиля {profile.Name}.";
            if (profile.Layers.Count is < 1 or > 10) return $"Профиль {profile.Name}: должно быть 1–10 слоёв.";
            foreach (var layer in profile.Layers)
            {
                if (string.IsNullOrWhiteSpace(layer.Name)) return $"Профиль {profile.Name}: задайте имя слоя.";
                foreach (var binding in layer.Bindings)
                {
                    var where = $"{profile.Name} / {layer.Name} / {binding.Source}";
                    if (!MouseMappingNames.Sources.Contains(binding.Source) || !MouseMappingNames.Triggers.Contains(binding.Trigger) ||
                        !MouseMappingNames.Actions.Contains(binding.Action)) return $"{where}: неверная кнопка, условие или действие.";
                    if (binding.DelayMs is < 20 or > 60000) return $"{where}: задержка должна быть 20–60000 мс.";
                    if (MouseMappingNames.IsWheel(binding.Source) && binding.Trigger is not "Нажатие")
                        return $"{where}: для колеса доступно только срабатывание при событии прокрутки.";
                    if (binding.Trigger == "Сочетание" && (binding.ChordSource == binding.Source ||
                        !MouseMappingNames.Sources.Contains(binding.ChordSource) || MouseMappingNames.IsWheel(binding.ChordSource)))
                        return $"{where}: выберите другую удерживаемую кнопку мыши.";
                    if (binding.Action == "Клавиши")
                    {
                        try
                        {
                            var hasChord = false;
                            foreach (var token in binding.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                                if (!(token.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
                                    int.TryParse(token[..^2], out var wait) && wait is >= 0 and <= 10000))
                                {
                                    MouseKeySequence.ParseChord(token);
                                    hasChord = true;
                                }
                            if (!hasChord) return $"{where}: укажите клавиши, а не только задержку.";
                            if (binding.Trigger is "Удержание" or "Переключатель" && binding.Value.Contains(','))
                                return $"{where}: для удержания и переключателя укажите одно сочетание без запятых.";
                            if (binding.Trigger is "Удержание" or "Переключатель")
                                MouseKeySequence.ParseChord(binding.Value);
                        }
                        catch (Exception error) { return $"{where}: {error.Message}"; }
                    }
                    if (binding.Action == "Кнопка мыши" && binding.Value is not ("ЛКМ" or "ПКМ" or "СКМ" or "Кнопка 4" or "Кнопка 5"))
                        return $"{where}: укажите ЛКМ, ПКМ, СКМ, Кнопка 4 или Кнопка 5.";
                    if (binding.Action is "Переключить слой" or "Слой пока удерживается" &&
                        !layerTarget(profile, binding.Value)) return $"{where}: укажите номер или имя существующего слоя.";
                    if (binding.Action == "Слой пока удерживается" && binding.Trigger is not ("Нажатие" or "Удержание"))
                        return $"{where}: временный слой назначьте на нажатие или удержание кнопки.";
                    if (binding.Action == "Переключить слой" && binding.Trigger == "Переключатель")
                        return $"{where}: для постоянной смены слоя выберите «Нажатие»; для временной — «Слой пока удерживается».";
                }
                foreach (var source in MouseMappingNames.Sources)
                    if (layer.Bindings.Count(binding => binding.Source == source && binding.Trigger == "Долгое удержание") > 4)
                        return $"{profile.Name} / {layer.Name}: не более четырёх долгих порогов для {source}.";
            }
        }
        return null;
    }

    private static bool layerTarget(MouseMappingProfile profile, string name) =>
        int.TryParse(name, out var number) ? number >= 1 && number <= profile.Layers.Count :
            profile.Layers.Any(layer => layer.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
