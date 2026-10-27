namespace KlikerBlocks;

internal static class VirtualKeyCatalog
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "Левая кнопка мыши", [2] = "Правая кнопка мыши", [3] = "Отмена", [4] = "Средняя кнопка мыши",
        [5] = "Боковая кнопка мыши X1", [6] = "Боковая кнопка мыши X2", [8] = "Backspace", [9] = "Tab",
        [12] = "Clear", [13] = "Enter", [16] = "Shift", [17] = "Ctrl", [18] = "Alt", [19] = "Pause",
        [20] = "Caps Lock", [21] = "IME Kana", [23] = "IME Junja", [24] = "IME Final", [25] = "IME Hanja",
        [27] = "Esc", [28] = "IME Convert", [29] = "IME Nonconvert", [30] = "IME Accept", [31] = "IME Mode Change",
        [32] = "Пробел", [33] = "Page Up", [34] = "Page Down", [35] = "End", [36] = "Home",
        [37] = "Стрелка влево", [38] = "Стрелка вверх", [39] = "Стрелка вправо", [40] = "Стрелка вниз",
        [41] = "Select", [42] = "Print", [43] = "Execute", [44] = "Print Screen", [45] = "Insert", [46] = "Delete",
        [47] = "Help", [91] = "Левая Windows", [92] = "Правая Windows", [93] = "Меню приложений", [95] = "Sleep",
        [106] = "Num *", [107] = "Num +", [108] = "Num Separator", [109] = "Num -", [110] = "Num .", [111] = "Num /",
        [144] = "Num Lock", [145] = "Scroll Lock", [160] = "Левый Shift", [161] = "Правый Shift",
        [162] = "Левый Ctrl", [163] = "Правый Ctrl", [164] = "Левый Alt", [165] = "Правый Alt",
        [166] = "Браузер: назад", [167] = "Браузер: вперёд", [168] = "Браузер: обновить",
        [169] = "Браузер: стоп", [170] = "Браузер: поиск", [171] = "Браузер: избранное", [172] = "Браузер: домой",
        [173] = "Без звука", [174] = "Громкость −", [175] = "Громкость +", [176] = "Следующий трек",
        [177] = "Предыдущий трек", [178] = "Стоп мультимедиа", [179] = "Воспроизведение/пауза",
        [180] = "Почта", [181] = "Выбор медиа", [182] = "Приложение 1", [183] = "Приложение 2",
        [186] = "OEM ;/: (зависит от раскладки)", [187] = "OEM +/= (зависит от раскладки)",
        [188] = "OEM ,/< (зависит от раскладки)", [189] = "OEM -/_ (зависит от раскладки)",
        [190] = "OEM ./> (зависит от раскладки)", [191] = "OEM /? (зависит от раскладки)",
        [192] = "OEM `~ (зависит от раскладки)", [219] = "OEM [{ (зависит от раскладки)",
        [220] = "OEM \\| (зависит от раскладки)", [221] = "OEM ]} (зависит от раскладки)",
        [222] = "OEM '\" (зависит от раскладки)", [223] = "OEM 8 (зависит от раскладки)",
        [226] = "OEM 102 (зависит от клавиатуры)", [229] = "IME Process", [231] = "Packet/Unicode",
        [246] = "Attn", [247] = "CrSel", [248] = "ExSel", [249] = "Erase EOF", [250] = "Play", [251] = "Zoom",
        [252] = "Без имени", [253] = "PA1", [254] = "OEM Clear"
    };

    public static string Describe(int code)
    {
        if (code is >= 48 and <= 57) return $"Цифра {(char)code} (верхний ряд)";
        if (code is >= 65 and <= 90) return $"Буква {(char)code} (клавиша независимо от регистра)";
        if (code is >= 96 and <= 105) return $"Num {code - 96}";
        if (code is >= 112 and <= 135) return $"F{code - 111}";
        return Names.GetValueOrDefault(code, "Не назначен / зарезервирован");
    }

    public static string DescribeEnglish(int code)
    {
        if (code is >= 48 and <= 57) return $"Digit {(char)code} (top row)";
        if (code is >= 65 and <= 90) return $"Letter {(char)code}";
        if (code is >= 96 and <= 105) return $"Numpad {code - 96}";
        if (code is >= 112 and <= 135) return $"F{code - 111}";
        return code switch
        {
            1 => "Left mouse button", 2 => "Right mouse button", 3 => "Cancel",
            4 => "Middle mouse button", 5 => "X1 mouse button", 6 => "X2 mouse button",
            32 => "Space", 37 => "Left arrow", 38 => "Up arrow", 39 => "Right arrow",
            40 => "Down arrow", 91 => "Left Windows", 92 => "Right Windows",
            93 => "Application menu", 252 => "Unnamed",
            _ => Enum.GetName((Keys)code) ?? "Unassigned / reserved"
        };
    }

    public static string DescribeLocalized(int code)
    {
        if (UiLanguage.Code == "ru") return Describe(code);
        if (UiLanguage.Code == "en") return DescribeEnglish(code);
        var original = Describe(code);
        if (UiLanguage.Has(original)) return UiLanguage.T(original);
        if (code is >= 48 and <= 57)
            return UiLanguage.Code switch { "fr" => $"Chiffre {(char)code}",
                "es" => $"Dígito {(char)code}", _ => $"数字 {(char)code}" };
        if (code is >= 65 and <= 90)
            return UiLanguage.Code switch { "fr" => $"Lettre {(char)code}",
                "es" => $"Letra {(char)code}", _ => $"字母 {(char)code}" };
        return DescribeEnglish(code);
    }

    public static string InputName(int code, bool allowMouse)
    {
        if (allowMouse && code is 1 or 2 or 4)
            return code switch { 1 => "LMB", 2 => "RMB", _ => "MMB" };
        if (allowMouse && code is >= 48 and <= 57) return $"D{(char)code}";
        if (code is >= 48 and <= 57 or >= 65 and <= 90) return ((char)code).ToString();
        if (code is >= 112 and <= 135) return $"F{code - 111}";
        return code switch
        {
            8 => "BACKSPACE", 9 => "TAB", 13 => "ENTER", 16 => "SHIFT", 17 => "CTRL",
            18 => "ALT", 27 => "ESC", 32 => "SPACE", 33 => "PAGEUP", 34 => "PAGEDOWN",
            35 => "END", 36 => "HOME", 37 => "LEFT", 38 => "UP", 39 => "RIGHT", 40 => "DOWN",
            45 => "INSERT", 46 => "DELETE", 91 => "WIN", _ => code.ToString()
        };
    }

    public static IEnumerable<(int Code, string Name)> AllCodes()
    {
        for (var code = 1; code <= 255; code++)
            yield return (code, Describe(code));
    }
}

internal sealed class VirtualKeyHelpForm : Form
{
    public VirtualKeyHelpForm()
    {
        Text = UiLanguage.T("Справка: коды клавиш Windows VK");
        Width = 600;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);
        var search = new TextBox { Dock = DockStyle.Fill, PlaceholderText = UiLanguage.T("Поиск: код, буква или название клавиши") };
        root.Controls.Add(search, 0, 0);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.Columns.Add("Code", UiLanguage.T("Код (десятичный)"));
        grid.Columns.Add("Hex", "Hex");
        grid.Columns.Add("Name", UiLanguage.T("Клавиша"));
        grid.Columns[0].FillWeight = 32;
        grid.Columns[1].FillWeight = 18;
        grid.Columns[2].FillWeight = 100;
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = UiLanguage.T("VK-коды 1–255. Для букв и цифр используйте коды из таблицы. Клавиши OEM зависят от раскладки и модели клавиатуры."),
            ForeColor = Color.FromArgb(60, 70, 90)
        }, 0, 2);
        void Filter()
        {
            grid.Rows.Clear();
            var query = search.Text.Trim();
            foreach (var (code, _) in VirtualKeyCatalog.AllCodes())
            {
                var name = VirtualKeyCatalog.DescribeLocalized(code);
                if (query.Length == 0 || code.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    $"0x{code:X2}".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    grid.Rows.Add(code, $"0x{code:X2}", name);
            }
        }
        search.TextChanged += (_, _) => Filter();
        Filter();
    }
}
