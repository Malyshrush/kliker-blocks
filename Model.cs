using System.Drawing;
using System.Text.Json;

namespace KlikerBlocks;

public sealed class ProjectModel
{
    public int Version { get; set; } = 1;
    public string ScriptName { get; set; } = "";
    public List<BlockNode> Nodes { get; set; } = [];
    public List<CaptureAsset> Captures { get; set; } = [];
    public List<SavedPoint> Points { get; set; } = [];
    public List<MacroRecording> Recordings { get; set; } = [];
    public bool ShowIndicator { get; set; } = true;
    public bool UseCustomIndicatorPosition { get; set; }
    public int IndicatorX { get; set; }
    public int IndicatorY { get; set; }
    public IndicatorAppearance IndicatorAppearance { get; set; } = new();
    public string IndicatorToggleHotkey { get; set; } = "Ctrl+Shift+I";
    public string RunMode { get; set; } = "Один проход";
    public int CycleDelayMs { get; set; } = 30;
    public decimal PlaybackSpeed { get; set; } = 1.0m;
    public WindowScopeSettings WindowScope { get; set; } = new();
    public MouseMappingSettings MouseMappings { get; set; } = new();
    // Old .klb files without this flag continue to read Unicode identifiers.
    public bool VariableNamesAsciiOnly { get; set; }
    public bool WriteJournalToFile { get; set; }
    public string JournalFileName { get; set; } = "kliker-journal.txt";

    public static ProjectModel CreateSample()
    {
        var start = BlockCatalog.Create("Start", 110, 130);
        var watch = BlockCatalog.Create("WatchCursor", 390, 130);
        start.Links["Далее"] = watch.Id;
        return new ProjectModel { Nodes = [start, watch], VariableNamesAsciiOnly = true };
    }

    public ProjectModel Copy() => JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(this))!;
}

public sealed class WindowScopeSettings
{
    public bool Enabled { get; set; }
    public string MatchMode { get; set; } = "Процесс";
    public string ProcessName { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public long WindowHandle { get; set; }
    public int ProcessId { get; set; }

    public WindowScopeSettings Copy() => (WindowScopeSettings)MemberwiseClone();
}

public sealed class IndicatorAppearance
{
    public int Width { get; set; } = 228;
    public int Height { get; set; } = 62;
    public string WindowShape { get; set; } = "Скруглённый прямоугольник";
    public int SymbolSize { get; set; } = 20;
    public string Symbol { get; set; } = "Круг";
    public bool ShowTitle { get; set; } = true;
    public bool ShowDetail { get; set; } = true;
    public bool ShowRunningTitle { get; set; } = true;
    public bool ShowActiveTitle { get; set; } = true;
    public bool ShowInactiveTitle { get; set; } = true;
    public string RunningTitle { get; set; } = "СЦЕНАРИЙ ВЫПОЛНЯЕТСЯ";
    public string ActiveTitle { get; set; } = "СЛЕЖЕНИЕ АКТИВНО";
    public string InactiveTitle { get; set; } = "СЛЕЖЕНИЕ НЕАКТИВНО";
    public string BackgroundColor { get; set; } = "#1E242D";
    public int BackgroundOpacityPercent { get; set; } = 100;
    public string RunningColor { get; set; } = "#4B9DE8";
    public int RunningOpacityPercent { get; set; } = 100;
    public string ActiveColor { get; set; } = "#41D674";
    public int ActiveOpacityPercent { get; set; } = 100;
    public string InactiveColor { get; set; } = "#EC5353";
    public int InactiveOpacityPercent { get; set; } = 100;
    public string BackgroundMode { get; set; } = "Цвет";
    public string RunningMode { get; set; } = "Цвет";
    public string ActiveMode { get; set; } = "Цвет";
    public string InactiveMode { get; set; } = "Цвет";
    public string BackgroundImagePath { get; set; } = "";
    public string RunningImagePath { get; set; } = "";
    public string ActiveImagePath { get; set; } = "";
    public string InactiveImagePath { get; set; } = "";
    // Старое единое изображение значка: переносится в настройки состояний при редактировании.
    public string ImagePath { get; set; } = "";

    public IndicatorAppearance Copy() => (IndicatorAppearance)MemberwiseClone();

    public Size BaseWindowSize()
    {
        var width = Math.Clamp(Width, 24, 800);
        var height = Math.Clamp(Height, 24, 300);
        if (WindowShape is "Квадрат" or "Круг" or "Скруглённый квадрат")
            width = height = Math.Min(width, height);
        return new Size(width, height);
    }

    public bool SymbolDominates() => Math.Clamp(SymbolSize, 6, 160) >=
        Math.Min(BaseWindowSize().Width, BaseWindowSize().Height);

    public Size EffectiveSize()
    {
        var side = Math.Clamp(SymbolSize, 6, 160);
        return SymbolDominates() ? new Size(side, side) : BaseWindowSize();
    }

    public string EffectiveWindowShape() => SymbolDominates() ? Symbol switch
    {
        "Квадрат" => "Квадрат",
        "Ромб" => "Ромб",
        _ => "Круг"
    } : WindowShape;

    public static Color ParseColor(string value, Color fallback)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch { return fallback; }
    }
}

public sealed class CaptureAsset
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Shape { get; set; } = "Прямоугольник";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
    public override string ToString() => $"{Name}  ({Shape}, {Width}×{Height})";
}

public sealed class SavedPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
    public override string ToString() => $"{X}, {Y}  [{CapturedAt:HH:mm:ss}]";
}

public sealed class MacroRecording
{
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "Клики и образы";
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public List<MacroEvent> Events { get; set; } = [];
    public int SearchPadding { get; set; } = 20;
    public override string ToString() => $"{Name} ({UiLanguage.T(Mode)}, {Events.Count} {UiLanguage.T("событий")})";
}

public sealed class MacroEvent
{
    public string Kind { get; set; } = "";
    public int DelayMs { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Button { get; set; } = "Левая";
    public string Key { get; set; } = "";
    public int WheelSteps { get; set; }
    public string CapturePath { get; set; } = "";
    public int CaptureX { get; set; }
    public int CaptureY { get; set; }
    public int CaptureWidth { get; set; }
    public int CaptureHeight { get; set; }
    public string WindowTitle { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string ElementName { get; set; } = "";
    public string ControlType { get; set; } = "";
}

public sealed class BlockNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public bool LogActions { get; set; }
    public bool Breakpoint { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
    public Dictionary<string, Guid?> Links { get; set; } = [];
    public Dictionary<string, LinkAppearance> LinkStyles { get; set; } = [];
    public BlockAppearance? Appearance { get; set; }

    public string Value(string key, string fallback = "") => Values.GetValueOrDefault(key, fallback);
    public int Int(string key, int fallback = 0)
    {
        if (!Values.TryGetValue(key, out var text)) return fallback;
        return int.TryParse(text, out var number) ? number
            : throw new InvalidOperationException($"Поле «{key}» должно содержать целое число, получено: «{text}».");
    }
    public Guid? Link(string name) => Links.GetValueOrDefault(name);
}

public sealed record FieldSpec(string Key, string Label, string Default, string[]? Options = null);
public sealed record BlockSpec(string Kind, string Title, string Hint, Color Color, string[] Outputs, FieldSpec[] Fields);

public static class BlockCatalog
{
    public const int Width = 196;
    public static int Height(BlockNode node) => node.Kind == "Description"
        ? DescriptionGeometry.Bounds(node).Height
        : BlockAppearanceManager.Resolve(node).Shape is "Square" or "Circle" or "Diamond"
            ? BlockAppearanceManager.Resolve(node).Width : 54 + 25 * Math.Max(1, Get(node.Kind).Outputs.Length);
    public static Rectangle Bounds(BlockNode node) => node.Kind == "Description"
        ? DescriptionGeometry.Bounds(node)
        : new Rectangle(node.X, node.Y, BlockAppearanceManager.Resolve(node).Width, Height(node));

    public static readonly BlockSpec[] All =
    [
        new("Start", "Старт", "Начало сценария", Color.FromArgb(54, 108, 90), ["Далее"], []),
        new("ShowcaseStep", "Шаг демонстрации", "Управляет встроенной демонстрацией «Что я умею»", Color.FromArgb(105, 77, 153), ["Далее"],
        [new("Action", "Действие демонстрации", "Intro"), new("Caption", "Подпись на графе", "Шаг демонстрации")]),
        new("Description", "Описание", "Область для пояснения группы блоков", Color.FromArgb(154, 199, 235), [],
        [new("Title", "Название области", "Описание сценария"),
         new("Width", "Ширина", "700"), new("Height", "Высота", "260"),
         new("FillColor", "Цвет заливки (#RRGGBB)", "#A8D9FF"),
         new("OpacityPercent", "Непрозрачность заливки, %", "35")]),
        new("WatchCursor", "Следить за курсором", "Возврат при смещении", Color.FromArgb(68, 95, 155), ["Далее"],
        [
            new("ActivationMode", "Активация", "Переключатель", ["Сразу", "Переключатель", "Пока удерживается"]),
            new("ActivationKey", "Клавиша управления (например F9)", "F9"),
            new("RequiredHold", "Дополнительно удерживать", "Левая кнопка мыши", ["Нет", "Левая кнопка мыши", "Правая кнопка мыши", "Средняя кнопка мыши", "Клавиша"]),
            new("RequiredKey", "Дополнительная клавиша", "SHIFT"),
            new("Anchor", "Опорная точка", "Центр монитора", ["Центр монитора", "Текущая точка", "Задать X/Y"]),
            new("AnchorX", "Опора X", "0"), new("AnchorY", "Опора Y", "0"),
            new("ThresholdPx", "Порог, пиксели", "3"), new("PollMs", "Проверка, мс", "10"),
            new("CooldownMs", "Пауза после возврата, мс", "60"),
            new("DurationMs", "Длительность, мс (0 = пока не остановить)", "0"),
            new("TargetMode", "Куда возвращать", "Центр монитора", ["Центр монитора", "Задать X/Y", "От смещённой точки", "От опоры"]),
            new("TargetX", "Цель X", "0"), new("TargetY", "Цель Y", "0"),
            new("OffsetX", "Смещение X", "0"), new("OffsetY", "Смещение Y", "0")
        ]),
        new("MoveCursor", "Переместить курсор", "Разовое перемещение", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("TargetMode", "Куда переместить", "Центр монитора", ["Центр монитора", "Задать X/Y", "От смещённой точки", "От опоры"]),
         new("TargetX", "Цель X", "0"), new("TargetY", "Цель Y", "0"), new("OffsetX", "Смещение X", "0"), new("OffsetY", "Смещение Y", "0")]),
        new("MoveRelative", "Сдвиг мыши", "Относительный ввод Windows", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("DeltaX", "Сдвиг X (+ вправо)", "0"), new("DeltaY", "Сдвиг Y (+ вниз)", "1"),
         new("DelayMs", "Задержка до сдвига, мс", "0"),
         new("DurationMs", "Время плавного сдвига, мс", "0"),
         new("Steps", "Шагов (1 = одним движением)", "1")]),
        new("Click", "Клик мышью", "В текущей точке", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Button", "Кнопка", "Левая", ["Левая", "Правая", "Средняя"]), new("Count", "Количество", "1")]),
        new("MouseButton", "Кнопка мыши", "Нажать или отпустить", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Button", "Кнопка", "Левая", ["Левая", "Правая", "Средняя"]), new("Action", "Действие", "Нажать", ["Нажать", "Отпустить"])]),
        new("Wheel", "Колесо мыши", "Прокрутка вверх/вниз", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Direction", "Направление", "Вниз", ["Вниз", "Вверх"]), new("Steps", "Шагов", "1")]),
        new("Key", "Нажать клавишу", "Например A, ENTER, F1", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Key", "Клавиша", "A")]),
        new("KeyState", "Клавиша: удержание", "Нажать или отпустить", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Key", "Клавиша", "CTRL"), new("Action", "Действие", "Нажать", ["Нажать", "Отпустить"])]),
        new("VirtualKey", "Код клавиши", "Для полной записи макроса", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("VirtualKeyCode", "Код Windows VK (1–255)", "65"), new("Action", "Действие", "Нажать", ["Нажать", "Отпустить"])]),
        new("TypeText", "Ввести текст", "Unicode-текст в активное окно", Color.FromArgb(133, 83, 130), ["Далее"],
        [new("Text", "Текст", "Привет!")]),
        new("ClipboardSet", "Буфер обмена", "Записать текст в буфер", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Текст", "Пример")]),
        new("ClipboardGet", "Прочитать буфер", "Текст из буфера в переменную", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Name", "Имя переменной", "text")]),
        new("TextFile", "Текстовый файл", "Прочитать, записать, очистить или удалить файл", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к UTF-8 файлу", "data.txt"),
         new("Action", "Операция", "Прочитать строку", ["Прочитать строку", "Количество строк", "Записать строку", "Вставить строку", "Добавить строку", "Добавить или перезаписать строкой", "Удалить строку", "Очистить", "Очистить или удалить файл", "Удалить файл"]),
         new("Line", "Номер строки (от 1)", "1"), new("Value", "Записываемый текст", ""),
         new("Rewrite", "Перезаписать файл (только для STRWRITELN: 0 или 1)", "0"),
         new("DeleteFile", "Удалить файл (только для TFCLEAR: 0 или 1)", "0"),
         new("Result", "Переменная результата", "result")]),
        new("TextFileArray", "Файл ↔ массив", "TFREADARR/TFWRITEARR: по одной строке на элемент", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к UTF-8 файлу", "data.txt"),
         new("Action", "Операция", "Прочитать в массив", ["Прочитать в массив", "Записать массив"]),
         new("Array", "Имя массива", "lines"), new("Count", "Переменная количества", "count")]),
        new("CountTextChars", "Посчитать символы в тексте", "Подсчёт символов с фильтрами", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Исходный текст", "111 22 A"), new("Mode", "Какие символы считать", "Все символы", ["Все символы", "Только из списка"]),
         new("Include", "Считать символы (по одному в строке)", ""),
         new("Exclude", "Исключить символы (по одному в строке)", ""),
         new("Spaces", "Считать пробелы", "Да", ["Да", "Нет"]),
         new("Tabs", "Считать табуляцию", "Да", ["Да", "Нет"]),
         new("Newlines", "Считать переносы строк", "Нет", ["Да", "Нет"]),
         new("Controls", "Считать непечатаемые символы", "Нет", ["Да", "Нет"]),
         new("Case", "Учитывать регистр", "Да", ["Да", "Нет"]), new("Result", "Переменная результата", "character_count")]),
        new("CountFileChars", "Посчитать символы в файле", "Подсчёт символов файла и строк", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к текстовому файлу", "data.txt"), new("Scope", "Область файла", "Весь файл", ["Весь файл", "Одна строка", "Диапазон строк"]),
         new("FirstLine", "Первая строка (от 1)", "1"), new("LastLine", "Последняя строка", "1"), new("Mode", "Какие символы считать", "Все символы", ["Все символы", "Только из списка"]),
         new("Include", "Считать символы (по одному в строке)", ""),
         new("Exclude", "Исключить символы (по одному в строке)", ""),
         new("Spaces", "Считать пробелы", "Да", ["Да", "Нет"]),
         new("Tabs", "Считать табуляцию", "Да", ["Да", "Нет"]),
         new("Newlines", "Считать переносы строк", "Нет", ["Да", "Нет"]),
         new("Controls", "Считать непечатаемые символы", "Нет", ["Да", "Нет"]),
         new("Case", "Учитывать регистр", "Да", ["Да", "Нет"]),
         new("Encoding", "Кодировка файла", "Авто", ["Авто", "UTF-8", "UTF-16 LE", "UTF-16 BE", "Windows-1251"]),
         new("Array", "Массив количества по строкам (пусто = нет)", ""), new("Result", "Переменная результата", "character_count")]),
        new("EditFileLine", "Изменить строку в файле", "Редактирование выбранной строки", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к текстовому файлу", "data.txt"), new("FirstLine", "Первая строка (от 1)", "1"), new("Action", "Действие с текстом", "Убрать повторные пробелы", ["Убрать повторные пробелы", "Убрать пробелы по краям", "Заменить текст", "Удалить символы", "Заменить полностью", "Удалить строки по условию"]),
         new("Find", "Найти текст", ""), new("Replace", "Новый текст", ""),
         new("Exclude", "Удалить символы (по одному в строке)", ""),
         new("Empty", "Удалять пустые строки", "Нет", ["Да", "Нет"]),
         new("Whitespace", "Удалять строки из пробелов и табуляции", "Нет", ["Да", "Нет"]),
         new("Repeated", "Удалять строки из повторяющегося символа", "Нет", ["Да", "Нет"]),
         new("RepeatSymbol", "Повторяемый символ (пусто = любой)", ""),
         new("MinRepeat", "Минимум повторений (от 1)", "2"),
         new("OutputPath", "Выходной файл (пусто = исходный)", ""),
         new("Encoding", "Кодировка файла", "Авто", ["Авто", "UTF-8", "UTF-16 LE", "UTF-16 BE", "Windows-1251"]),
         new("Result", "Переменная результата", "text_result")]),
        new("EditFileText", "Изменить текст в файле", "Редактирование строк или всего текста", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к текстовому файлу", "data.txt"), new("Scope", "Область файла", "Весь файл", ["Весь файл", "Одна строка", "Диапазон строк"]),
         new("FirstLine", "Первая строка (от 1)", "1"), new("LastLine", "Последняя строка", "1"),
         new("Processing", "Обрабатывать", "Построчно", ["Построчно", "Как единый текст"]), new("Action", "Действие с текстом", "Убрать повторные пробелы", ["Убрать повторные пробелы", "Убрать пробелы по краям", "Заменить текст", "Удалить символы", "Заменить полностью", "Удалить строки по условию"]),
         new("Find", "Найти текст", ""), new("Replace", "Новый текст", ""),
         new("Exclude", "Удалить символы (по одному в строке)", ""),
         new("Empty", "Удалять пустые строки", "Нет", ["Да", "Нет"]),
         new("Whitespace", "Удалять строки из пробелов и табуляции", "Нет", ["Да", "Нет"]),
         new("Repeated", "Удалять строки из повторяющегося символа", "Нет", ["Да", "Нет"]),
         new("RepeatSymbol", "Повторяемый символ (пусто = любой)", ""),
         new("MinRepeat", "Минимум повторений (от 1)", "2"),
         new("OutputPath", "Выходной файл (пусто = исходный)", ""),
         new("Encoding", "Кодировка файла", "Авто", ["Авто", "UTF-8", "UTF-16 LE", "UTF-16 BE", "Windows-1251"]),
         new("Result", "Переменная результата", "text_result")]),
        new("CsvTable", "Таблица CSV", "Ячейка, число строк или отбор строк", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к CSV-файлу", "data.csv"),
         new("Delimiter", "Разделитель CSV", ";", [";", ",", "TAB"]),
         new("Action", "Операция", "Прочитать ячейку", ["Прочитать ячейку", "Записать ячейку", "Количество строк", "Отобрать строки"]),
         new("Row", "Строка (1–10000)", "1"), new("Column", "Столбец (1–256)", "1"),
         new("Value", "Значение для записи", ""),
         new("FilterColumn", "Столбец фильтра (1–256)", "1"),
         new("FilterValue", "Искать текст в ячейке", ""),
         new("OutputPath", "Выходной CSV-файл для отбора", "selected.csv"),
         new("Result", "Переменная результата", "csv_result")]),
        new("ExcelTable", "Таблица Excel", "Ячейка, число строк и поиск в .xlsx", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к .xlsx", "data.xlsx"), new("Sheet", "Имя листа (пусто = первый)", ""),
         new("Action", "Операция", "Прочитать ячейку", ["Прочитать ячейку", "Записать ячейку", "Количество строк", "Найти строки"]),
         new("Row", "Строка Excel", "1"), new("Column", "Столбец Excel", "1"),
         new("Value", "Значение для записи", ""), new("FilterColumn", "Столбец поиска", "1"),
         new("FilterValue", "Искомый текст", ""), new("Result", "Переменная результата", "excel_result")]),
        new("PdfText", "Текст PDF", "Извлечение текста или число страниц", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к PDF", "document.pdf"),
         new("Action", "Операция", "Текст страницы", ["Текст страницы", "Весь текст", "Количество страниц"]),
         new("Page", "Номер страницы", "1"), new("Result", "Переменная результата", "pdf_result")]),
        new("FileQueue", "Очередь файлов", "Пакетная обработка с сохранением состояния", Color.FromArgb(70, 107, 112), ["Есть файл", "Очередь пуста", "Далее"],
        [new("Folder", "Папка входных файлов", "."), new("Mask", "Маска файлов", "*.txt"),
         new("StatePath", "Файл состояния очереди", "file-queue.json"),
         new("Action", "Операция", "Взять следующий", ["Взять следующий", "Отметить готовым", "Отметить ошибкой"]),
         new("CurrentFile", "Текущий файл для отметки", ""),
         new("Result", "Переменная пути файла", "queue_file")]),
        new("Checkpoint", "Контрольная точка", "Сохранить место и переменные для продолжения", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Файл контрольной точки", "checkpoint.json")]),
        new("IniFile", "Файл INI", "Чтение или запись параметра", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Путь к INI", "config.ini"), new("Section", "Секция", "default"),
         new("Key", "Ключ", "name"), new("Action", "Операция", "Прочитать", ["Прочитать", "Записать"]),
         new("Value", "Записываемый текст", ""), new("Result", "Переменная результата", "result")]),
        new("DirectoryList", "Список файлов", "Имена файлов или папок в массив", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Каталог", "."), new("Mask", "Маска", "*"),
         new("Pattern", "Путь с маской (необязательно; при заполнении заменяет два поля выше)", ""),
         new("Kind", "Что перечислить", "Файлы", ["Файлы", "Папки"]),
         new("Array", "Массив результата", "files"), new("Count", "Переменная количества", "count")]),
        new("HttpGet", "HTTP GET", "Получить текст по URL", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Url", "URL (HTTP/HTTPS)", "https://example.com"),
         new("Headers", "Заголовки: Имя: значение, каждый с новой строки (необязательно)", ""),
         new("TimeoutMs", "Таймаут, мс", "10000"), new("Result", "Переменная результата", "response")]),
        new("HttpPost", "HTTP POST", "Отправить текст или JSON на выбранный адрес", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Url", "URL (HTTP/HTTPS)", "https://example.com/api"),
         new("Body", "Тело запроса", "{}"),
         new("ContentType", "Тип содержимого", "application/json",
             ["application/json", "text/plain", "application/x-www-form-urlencoded"]),
         new("Headers", "Заголовки: Имя: значение, каждый с новой строки (необязательно)", ""),
         new("TimeoutMs", "Таймаут, мс", "10000"),
         new("Result", "Переменная ответа", "response"),
         new("Status", "Переменная кода HTTP", "http_status")]),
        new("JsonValue", "Поле JSON", "Извлечь поле из JSON-строки", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Json", "Текст JSON или ${переменная}", "{\"user\":{\"name\":\"Alex\"}}"),
         new("Path", "Путь к полю, например user.name или items[0].id", "user.name"),
         new("Result", "Переменная результата", "json_value")]),
        new("TranslateText", "Перевести текст", "Нужен сервис LibreTranslate или установленная локальная модель Argos", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Текст (поддерживает ${переменная})", "Hello, world!"),
         new("Source", "Исходный язык", "en", ["auto", "ru", "en", "fr", "es", "zh", "de"]),
         new("Target", "Язык перевода", "ru", ["ru", "en", "fr", "es", "zh", "de"]),
         new("Engine", "Движок перевода", "LibreTranslate", ["LibreTranslate", "Argos локально"]),
         new("Endpoint", "Адрес сервиса перевода", "http://127.0.0.1:5000/translate"),
         new("TimeoutMs", "Таймаут, мс", "60000"), new("Result", "Переменная результата", "translated")]),
        new("DetectLanguage", "Определить язык", "Код языка и уверенность через LibreTranslate", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Текст", "Bonjour!"),
         new("Endpoint", "Адрес сервиса перевода", "http://127.0.0.1:5000/translate"),
         new("TimeoutMs", "Таймаут, мс", "10000"),
         new("Result", "Переменная кода языка", "language"),
         new("Confidence", "Переменная уверенности", "confidence")]),
        new("LanguagePair", "Если перевод доступен", "Проверка пары на сервисе или в локальных моделях", Color.FromArgb(70, 107, 112), ["Да", "Нет"],
        [new("Source", "Исходный язык", "en", ["ru", "en", "fr", "es", "zh", "de"]),
         new("Target", "Язык перевода", "ru", ["ru", "en", "fr", "es", "zh", "de"]),
         new("Engine", "Движок перевода", "LibreTranslate", ["LibreTranslate", "Argos локально"]),
         new("Endpoint", "Адрес сервиса перевода", "http://127.0.0.1:5000/translate"),
         new("TimeoutMs", "Таймаут, мс", "10000")]),
        new("TranslateArray", "Перевести массив", "Последовательно перевести строки массива", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Input", "Исходный массив", "lines"), new("Output", "Массив перевода", "translations"),
         new("Source", "Исходный язык", "en", ["ru", "en", "fr", "es", "zh", "de"]),
         new("Target", "Язык перевода", "ru", ["ru", "en", "fr", "es", "zh", "de"]),
         new("Engine", "Движок перевода", "LibreTranslate", ["LibreTranslate", "Argos локально"]),
         new("Endpoint", "Адрес сервиса перевода", "http://127.0.0.1:5000/translate"),
         new("TimeoutMs", "Таймаут одной строки, мс", "60000"),
         new("Count", "Переменная количества", "count")]),
        new("Wait", "Пауза", "Ожидание в миллисекундах", Color.FromArgb(129, 105, 57), ["Далее"],
        [new("Milliseconds", "Миллисекунды", "500")]),
        new("WaitEvent", "Ожидать событие", "Появление или закрытие окна, изменение файла", Color.FromArgb(129, 105, 57), ["Событие", "Таймаут"],
        [new("EventType", "Тип события", "Окно появилось", ["Окно появилось", "Окно закрылось", "Файл изменился"]),
         new("WindowTitle", "Часть заголовка окна", "Блокнот"),
         new("Path", "Путь к отслеживаемому файлу", "watch.txt"),
         new("TimeoutMs", "Таймаут, мс (100–3600000)", "30000")]),
        new("InputGroup", "Ввод без прерывания", "Тело выполняется в общей очереди до «Конца ветки»; освобождение автоматическое", Color.FromArgb(76, 110, 146), ["Тело", "Далее"],
        [new("MaxMs", "Максимальное время группы, мс", "10000")]),
        new("SetVariable", "Задать переменную", "Имя и текстовое значение", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя (буквы, цифры, _)", "counter"), new("Value", "Значение", "0")]),
        new("DefineVariable", "Объявить переменную", "Задать начальное значение только если переменная отсутствует", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя", "counter"), new("Value", "Начальное значение", "0")]),
        new("UndefineVariable", "Удалить переменную", "UNDEFINE: удалить переменную и одноимённый массив", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя переменной или массива", "counter")]),
        new("SetVariableByName", "Задать по имени", "SETVAR: имя переменной берётся из строки", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Reference", "Имя или $имя (можно ${переменная} / =выражение)", "$counter"),
         new("Value", "Новое значение", "0")]),
        new("MathVariable", "Вычислить переменную", "Арифметика с числом", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя переменной", "counter"),
         new("Operation", "Операция", "Сложить", ["Сложить", "Вычесть", "Умножить", "Разделить"]),
         new("Operand", "Число или ${имя}", "1")]),
        new("ArraySet", "Элемент массива", "Задать значение по индексу", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя массива", "data"), new("Index", "Индекс (0 и выше)", "0"),
         new("Value", "Значение (выражение начинается с =)", "0")]),
        new("ArrayAction", "Операция с массивом", "Добавить, извлечь, размер или сортировка", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Name", "Имя массива", "data"),
         new("Action", "Операция", "Добавить", ["Добавить", "Извлечь", "Размер", "Сортировать текст", "Сортировать числа", "Сортировать автоматически"]),
         new("Value", "Значение для добавления", "0"), new("Result", "Переменная результата", "result")]),
        new("StringSplit", "Разделить строку", "STRSEPARATE: части строки в массив", Color.FromArgb(66, 119, 107), ["Далее"],
        [new("Text", "Исходная строка", "один,два,три"), new("Separator", "Разделитель", ","),
         new("Array", "Массив частей", "parts"), new("Count", "Переменная количества", "count")]),
        new("IfVariable", "Если переменная", "Сравнить значение", Color.FromArgb(66, 119, 107), ["Да", "Нет"],
        [new("Name", "Имя переменной", "counter"),
         new("Comparison", "Сравнение", "Равно", ["Равно", "Не равно", "Больше", "Не меньше", "Меньше", "Не больше", "Содержит"]),
         new("Value", "С чем сравнить", "10"),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Ignore,
             [IndicatorConditionMode.Ignore, IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted])]),
        new("IfExpression", "Если выражение", "Ненулевой результат ведёт по ветке Да", Color.FromArgb(66, 119, 107), ["Да", "Нет"],
        [new("Expression", "Выражение (начинается с =)", "=1==1"),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Ignore,
             [IndicatorConditionMode.Ignore, IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted])]),
        new("WhileExpression", "Пока условие", "Повторять тело до ложного результата или остановки", Color.FromArgb(116, 99, 53), ["Тело", "Далее"],
        [new("Expression", "Выражение (начинается с =)", "=$counter<10"),
         new("MaxIterations", "Максимум проходов (0 = без лимита)", "100000")]),
        new("SwitchValue", "Выбор", "Первое совпадение из пяти случаев", Color.FromArgb(116, 99, 53),
        ["Случай 1", "Случай 2", "Случай 3", "Случай 4", "Случай 5", "Иначе"],
        [new("Expression", "Проверяемое значение", "=$counter"),
         new("Case1", "Случай 1 (пусто = пропуск)", "1"), new("Case2", "Случай 2", "2"),
         new("Case3", "Случай 3", ""), new("Case4", "Случай 4", ""), new("Case5", "Случай 5", "")]),
        new("IfMoved", "Если курсор смещён", "Проверка в один момент", Color.FromArgb(119, 91, 57), ["Да", "Нет"],
        [new("Anchor", "Опорная точка", "Центр монитора", ["Центр монитора", "Задать X/Y"]),
         new("AnchorX", "Опора X", "0"), new("AnchorY", "Опора Y", "0"), new("ThresholdPx", "Порог, пиксели", "3"),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Ignore,
             [IndicatorConditionMode.Ignore, IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted])]),
        new("IfPixel", "Если цвет пикселя", "Сравнить цвет в X/Y", Color.FromArgb(119, 91, 57), ["Да", "Нет"],
        [new("PixelX", "Экран X", "0"), new("PixelY", "Экран Y", "0"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0"),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Ignore,
             [IndicatorConditionMode.Ignore, IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted])]),
        new("WaitPixel", "Ждать цвет пикселя", "До совпадения или таймаута", Color.FromArgb(119, 91, 57), ["Найден", "Таймаут"],
        [new("PixelX", "Экран X", "0"), new("PixelY", "Экран Y", "0"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0"),
         new("PollMs", "Интервал, мс", "100"), new("TimeoutMs", "Таймаут, мс (0 = без лимита)", "5000")]),
        new("IfKeyDown", "Если клавиша нажата", "Удержание, разовое нажатие или переключатель", Color.FromArgb(119, 91, 57), ["Да", "Нет"],
        [new("Key", "Клавиша", "SHIFT"), new("Mode", "Режим", "Пока удерживается", ["Пока удерживается", "При новом нажатии", "Переключатель"]),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Normal,
             [IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted, IndicatorConditionMode.Ignore])]),
        new("FindColor", "Найти цвет", "Первый пиксель в области", Color.FromArgb(119, 91, 57), ["Найден", "Нет"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0")]),
        new("FindImage", "Найти изображение", "Шаблон BMP/PNG", Color.FromArgb(119, 91, 57), ["Найден", "Нет"],
        [new("Path", "Файл шаблона", "template.png"), new("Left", "Левая граница", "0"),
         new("Top", "Верхняя граница", "0"), new("Right", "Правая граница", "500"),
         new("Bottom", "Нижняя граница", "500"), new("Tolerance", "Допуск 0–255", "0")]),
        new("Screenshot", "Снимок экрана", "Сохранить PNG-файл", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Path", "Файл PNG", "screen.png"), new("Left", "Левая граница", "0"),
         new("Top", "Верхняя граница", "0"), new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500")]),
        new("Note", "Заметка", "Создать или изменить экранную заметку", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Action", "Действие с заметкой", "Создать", ["Создать", "Открыть", "Изменить", "Прочитать", "Сохранить", "Свернуть", "Развернуть", "Закрыть", "Изменить файл", "Прочитать файл"]),
         new("Id", "Идентификатор заметки", "note1"), new("Path", "Файл заметки .klnote", ""),
         new("TextMode", "Изменение текста заметки", "Не менять", ["Не менять", "Заменить", "Добавить"]),
         new("Text", "Текст заметки", ""), new("ImageMode", "Изменение изображения заметки", "Не менять", ["Не менять", "Из файла", "Убрать"]),
         new("ImagePath", "Путь изображения заметки", ""), new("X", "X заметки (пусто = без изменения)", ""),
         new("Y", "Y заметки (пусто = без изменения)", ""), new("Width", "Ширина заметки (пусто = без изменения)", ""),
         new("Height", "Высота заметки (пусто = без изменения)", ""), new("Result", "Переменная результата заметки", "note_id")]),
        new("OcrImage", "Распознать текст", "Локальный OCR изображений и PDF", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Path", "Путь к изображению или PDF", "screen.png"),
         new("EnginePath", "Путь к tesseract.exe (пусто = поиск)", ""),
         new("Language", "Код языка OCR (например eng+rus)", "eng+rus"),
         new("PdfPage", "Страница PDF (номер; 0 = все, не более 20)", "0"),
         new("TimeoutMs", "Таймаут OCR, мс", "30000"),
         new("Result", "Переменная результата", "ocr_text")]),
        new("WaitScreenText", "Ожидать текст на экране", "Ищет текст с помощью локального OCR", Color.FromArgb(61, 116, 157), ["Найдено", "Таймаут"],
        [new("Text", "Искомый текст", "Готово"),
         new("AreaMode", "Область поиска", "Весь экран", ["Весь экран", "Задать область"]),
         new("Left", "Слева", "0"), new("Top", "Сверху", "0"), new("Width", "Ширина", "800"), new("Height", "Высота", "600"),
         new("Language", "Код языка OCR", "eng+rus"), new("Engine", "Путь к tesseract.exe", ""),
         new("TimeoutMs", "Таймаут, мс", "10000"), new("IntervalMs", "Интервал проверки, мс", "500"),
         new("Result", "Переменная распознанного текста", "screen_text")]),
        new("ExtractText", "Извлечь фрагмент текста", "Извлекает данные по регулярному выражению", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Исходный текст", "${source_text}"), new("Pattern", "Регулярное выражение", "(\\d+)"),
         new("Group", "Номер группы", "1"), new("Result", "Переменная результата", "extracted_text")]),
        new("BufferCapture", "Запомнить экран", "Снимок для повторного анализа, аналог GETSCREEN", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("AreaMode", "Область", "Задать область", ["Задать область", "Весь рабочий стол"]),
         new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница (не включена)", "500"), new("Bottom", "Нижняя граница (не включена)", "500")]),
        new("BufferPixel", "Цвет из снимка", "Прочитать один пиксель из запомненного экрана", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("PixelX", "Экран X", "0"), new("PixelY", "Экран Y", "0"),
         new("Format", "Формат результата", "COLORREF", ["COLORREF", "#RRGGBB"]),
         new("Result", "Переменная результата", "color")]),
        new("BufferCount", "Подсчитать цвет", "Число совпадающих пикселей в снимке", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0"),
         new("Result", "Переменная результата", "count")]),
        new("BufferHash", "Контрольная сумма снимка", "XOR или CRC32 области снимка", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Algorithm", "Алгоритм", "CRC32", ["CRC32", "XOR"]),
         new("Result", "Переменная результата", "checksum")]),
        new("BufferReplace", "Заменить цвет в снимке", "Меняет запомненный буфер, не настоящий экран", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("OldColor", "Старый цвет (-1 = любой)", "#FFFFFF"), new("NewColor", "Новый цвет", "#000000"),
         new("Result", "Переменная заменённых", "replaced")]),
        new("BufferFindColor", "Найти цвет в снимке", "Поиск по запомненному экрану", Color.FromArgb(119, 91, 57), ["Найден", "Нет"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0")]),
        new("BufferFindImage", "Найти изображение в снимке", "Поиск шаблона в запомненном экране", Color.FromArgb(119, 91, 57), ["Найден", "Нет"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Path", "Файл шаблона", "template.png"), new("Tolerance", "Допуск 0–255", "0"),
         new("IgnoreColor", "Игнорировать цвет (-1 = нет)", "-1")]),
        new("BufferScanColor", "Все пиксели цвета", "Координаты в массив X,Y,X,Y…", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Color", "Цвет #RRGGBB или COLORREF", "#FFFFFF"), new("Tolerance", "Допуск 0–255", "0"),
         new("ArrayName", "Имя массива координат", "points"), new("CountName", "Переменная числа точек", "found"),
         new("MaxResults", "Предел результатов", "1000")]),
        new("BufferScanImage", "Все совпадения картинки", "Координаты в массив X,Y,X,Y…", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Left", "Левая граница", "0"), new("Top", "Верхняя граница", "0"),
         new("Right", "Правая граница", "500"), new("Bottom", "Нижняя граница", "500"),
         new("Path", "Файл шаблона", "template.png"), new("Tolerance", "Допуск 0–255", "0"),
         new("IgnoreColor", "Игнорировать цвет (-1 = нет)", "-1"),
         new("ArrayName", "Имя массива координат", "points"), new("CountName", "Переменная числа точек", "found"),
         new("MaxResults", "Предел результатов", "1000")]),
        new("BufferSave", "Сохранить снимок", "Записать текущий буфер в PNG", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Path", "Файл PNG", "buffer.png")]),
        new("MoveFound", "К найденной точке", "Последний цвет/изображение", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("OffsetX", "Смещение X", "0"), new("OffsetY", "Смещение Y", "0")]),
        new("IfWindow", "Если окно найдено", "По части заголовка", Color.FromArgb(119, 91, 57), ["Да", "Нет"],
        [new("Title", "Часть заголовка", "Блокнот"),
         new("IndicatorMode", "Индикатор (только отображение)", IndicatorConditionMode.Ignore,
             [IndicatorConditionMode.Ignore, IndicatorConditionMode.Normal, IndicatorConditionMode.Inverted])]),
        new("ActivateWindow", "Активировать окно", "По части заголовка", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Title", "Часть заголовка", "Блокнот")]),
        new("MoveWindow", "Размер и позиция окна", "По части заголовка", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Title", "Часть заголовка", "Блокнот"), new("WindowX", "Левый X", "100"),
         new("WindowY", "Верхний Y", "100"), new("WindowWidth", "Ширина", "800"), new("WindowHeight", "Высота", "600")]),
        new("WindowFind", "Найти HWND окна", "По заголовку или его началу", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Title", "Заголовок или часть", "Блокнот"), new("PrefixOnly", "Только начало заголовка", "Нет", ["Нет", "Да"]),
         new("Result", "Переменная HWND (0 = не найдено)", "window")]),
        new("WindowInfo", "Геометрия окна", "Получить X, Y, ширину, высоту по HWND", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Handle", "HWND (число или переменная)", "${window}"),
         new("XName", "Переменная X", "window_x"), new("YName", "Переменная Y", "window_y"),
         new("WidthName", "Переменная ширины", "window_w"), new("HeightName", "Переменная высоты", "window_h")]),
        new("WindowAction", "Действие с окном", "Позиция, размер, состояние и активация по HWND", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Handle", "HWND (число или переменная)", "${window}"),
         new("Action", "Действие", "Позиция", ["Позиция", "Размер", "Геометрия", "Состояние", "Активировать"]),
         new("WindowX", "X", "100"), new("WindowY", "Y", "100"),
         new("WindowWidth", "Ширина", "800"), new("WindowHeight", "Высота", "600"),
         new("State", "Состояние (0 = свернуть, 1 = восстановить)", "1")]),
        new("ElementAtPoint", "Элемент в точке", "Найти HWND элемента под курсором/точкой", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("PointX", "X на экране", "100"), new("PointY", "Y на экране", "100"),
         new("Result", "Переменная HWND (0 = нет)", "element")]),
        new("ElementInfo", "Данные элемента", "Получить текст или PID по HWND", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Handle", "HWND (число или переменная)", "${element}"),
         new("Info", "Что прочитать", "Текст", ["Текст", "PID"]),
         new("Result", "Переменная результата", "element_data")]),
        new("ElementSetText", "Задать текст элемента", "WM_SETTEXT с таймаутом 500 мс", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Handle", "HWND (число или переменная)", "${element}"),
         new("Text", "Новый текст", "текст")]),
        new("UiWait", "Дождаться элемента", "Поиск элемента Windows по UI Automation", Color.FromArgb(61, 116, 157), ["Найден", "Таймаут"],
        [new("WindowTitle", "Часть заголовка окна (пусто = активное)", ""),
         new("AutomationId", "AutomationId элемента", ""),
         new("ElementName", "Имя элемента", ""),
         new("ControlType", "Тип элемента", "Button"),
         new("TimeoutMs", "Таймаут, мс", "10000")]),
        new("UiAction", "Действие с элементом", "Нажать, выбрать или изменить элемент Windows", Color.FromArgb(61, 116, 157), ["Далее", "Ошибка"],
        [new("WindowTitle", "Часть заголовка окна (пусто = активное)", ""),
         new("AutomationId", "AutomationId элемента", ""),
         new("ElementName", "Имя элемента", ""),
         new("ControlType", "Тип элемента", "Button"),
         new("Action", "Действие с элементом Windows", "Нажать",
             ["Нажать", "Ввести текст", "Переключить", "Выбрать", "Прочитать текст"]),
         new("Text", "Текст для ввода", ""),
         new("Result", "Переменная результата", "ui_result"),
         new("Error", "Переменная ошибки", "ui_error")]),
        new("MessageSend", "Послать сообщение окну", "SENDMESSAGE или POSTMESSAGE", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("Handle", "HWND (число или переменная)", "${window}"),
         new("Message", "Числовой код сообщения", "1024"),
         new("WParam", "wParam (целое число)", "0"), new("LParam", "lParam (целое число)", "0"),
         new("Mode", "Способ отправки", "Асинхронно", ["Асинхронно", "Синхронно"]),
         new("Result", "Переменная ответа (POST = 1)", "response")]),
        new("MessageReceive", "Принять сообщение", "Последнее WM_USER (1024) окну Kliker Blocks", Color.FromArgb(61, 116, 157), ["Далее"],
        [new("WName", "Переменная wParam", "wparam"), new("LName", "Переменная lParam", "lparam"),
         new("NewName", "Переменная нового сообщения 1/0", "is_new")]),
        new("RunFile", "Запустить файл", "EXE, BAT или документ", Color.FromArgb(74, 109, 100), ["Далее"],
        [new("Path", "Путь к файлу", "script.bat"), new("Arguments", "Аргументы", ""),
         new("WaitForExit", "Ждать завершения", "Нет", ["Нет", "Да"]), new("TimeoutMs", "Таймаут, мс (0 = без лимита)", "0")]),
        new("RunAhk", "AutoHotkey v2", "Запустить .ahk через v2", Color.FromArgb(74, 109, 100), ["Далее"],
        [new("Path", "Путь к .ahk", "script.ahk"), new("Interpreter", "AutoHotkey64.exe (пусто = поиск)", ""),
         new("Arguments", "Аргументы скрипта", ""), new("WaitForExit", "Ждать завершения", "Нет", ["Нет", "Да"]),
         new("TimeoutMs", "Таймаут, мс (0 = без лимита)", "0")]),
        new("RunScenario", "Запустить сценарий", "Выполнить сохранённый .klb последовательно", Color.FromArgb(74, 109, 100), ["Далее"],
        [new("Path", "Файл сценария .klb", ""), new("Times", "Повторений (1–1000)", "1"),
         new("Speed", "Скорость сценария (0,1–5,0×)", "1,0"),
         new("Inputs", "Входы: дочерняя_переменная=значение, по одному на строке", ""),
         new("Outputs", "Выходы: родительская_переменная=дочерняя_переменная, по одному на строке", "")]),
        new("Repeat", "Повторить", "Ветка тела завершается блоком «Конец ветки»", Color.FromArgb(116, 99, 53), ["Тело", "Далее"],
        [new("Times", "Повторений", "3")]),
        new("CallSub", "Вызвать подпрограмму", "Вызов по имени, затем возврат", Color.FromArgb(77, 101, 151), ["Далее"],
        [new("Name", "Имя подпрограммы", "Моя подпрограмма")]),
        new("SubStart", "Начало подпрограммы", "Отдельная цепочка, как SUB", Color.FromArgb(77, 101, 151), ["Тело"],
        [new("Name", "Имя подпрограммы", "Моя подпрограмма")]),
        new("SubReturn", "Возврат из подпрограммы", "Как END_SUB", Color.FromArgb(77, 101, 151), [], []),
        new("Parallel", "Параллельно", "Обе ветки завершаются блоком «Конец ветки»", Color.FromArgb(92, 85, 145), ["Ветка A", "Ветка B", "Далее"], []),
        new("BranchEnd", "Конец ветки", "Завершение цикла или параллельной ветки", Color.FromArgb(91, 91, 91), [], []),
        new("Log", "Сообщение", "Запись в журнал", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Текст", "Шаг выполнен")]),
        new("LogFile", "Файл журнала", "Дублировать сообщения в UTF-8 файл", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Имя файла в папке проекта; пусто — выключить", "script.log")]),
        new("LogClear", "Очистить журнал", "Удалить видимые записи", Color.FromArgb(70, 107, 112), ["Далее"], []),
        new("LogShow", "Показать журнал", "Показать или скрыть панель журнала", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Visible", "Показать: 1, скрыть: 0", "1")]),
        new("PlaySound", "Воспроизвести WAV", "Начать воспроизведение без ожидания", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Path", "Файл .wav", "signal.wav")]),
        new("SpeakText", "Озвучить текст", "Произнести текст голосом Windows", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Text", "Текст для озвучивания", ""), new("Voice", "Голос Windows (пусто = авто)", ""),
         new("Rate", "Скорость речи (-10…10)", "0"), new("Volume", "Громкость речи (0…100)", "100")]),
        new("Beep", "Звуковой сигнал", "Частота и длительность", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Frequency", "Частота, Гц (37–32767)", "440"), new("DurationMs", "Длительность, мс (1–5000)", "200")]),
        new("InputDialog", "Запросить текст", "Строка в переменную; отмена = пусто", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Message", "Вопрос", "Введите значение"), new("Default", "Значение по умолчанию", ""),
         new("DelaySeconds", "Таймаут без ввода, секунды (0 = ждать)", "5"),
         new("Result", "Переменная результата", "input"),
         new("WindowTitle", "Заголовок окна (пусто = стандартный)", ""),
         new("WindowX", "X окна (пусто = по центру)", ""), new("WindowY", "Y окна (пусто = по центру)", ""),
         new("WindowWidth", "Ширина окна (360–3000; пусто = авто)", ""),
         new("WindowHeight", "Высота окна (185–3000; пусто = авто)", "")]),
        new("ChoiceDialog", "Вопрос с кнопками", "Код нажатой кнопки в переменную", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Message", "Вопрос", "Продолжить?"), new("Buttons", "Кнопки в окне", "4", ["0", "1", "2", "3", "4", "5"]),
         new("Icon", "Иконка окна", "2", ["0", "1", "2", "3", "4"]), new("Result", "Переменная результата", "response"),
         new("WindowTitle", "Заголовок окна (пусто = стандартный)", ""),
         new("WindowX", "X окна (пусто = по центру)", ""), new("WindowY", "Y окна (пусто = по центру)", ""),
         new("WindowWidth", "Ширина окна (360–3000; пусто = авто)", ""),
         new("WindowHeight", "Высота окна (180–3000; пусто = авто)", "")]),
        new("RadioDialog", "Выбрать вариант", "RADIOBOX: номер варианта или 0", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Message", "Вопрос", "Выберите вариант"),
         new("Options", "Варианты: каждый с новой строки", "Первый\nВторой\nТретий"),
         new("Result", "Переменная результата", "choice"),
         new("WindowTitle", "Заголовок окна (пусто = стандартный)", ""),
         new("WindowX", "X окна (пусто = по центру)", ""), new("WindowY", "Y окна (пусто = по центру)", ""),
         new("WindowWidth", "Ширина окна (360–3000; пусто = авто)", ""),
         new("WindowHeight", "Высота окна (260–3000; пусто = авто)", "")]),
        new("HintPopup", "Уведомление Windows", "Всплывающее сообщение в области уведомлений", Color.FromArgb(70, 107, 112), ["Далее"],
        [new("Message", "Сообщение", "Сценарий выполнил шаг"), new("Title", "Заголовок", "Kliker Blocks")]),
        new("Stop", "Стоп", "Завершение сценария", Color.FromArgb(150, 66, 70), [], [])
    ];

    public static BlockSpec Get(string kind) => All.First(x => x.Kind == kind);

    public static BlockNode Create(string kind, int x, int y)
    {
        var spec = Get(kind);
        return new BlockNode
        {
            Kind = kind,
            X = x,
            Y = y,
            Values = spec.Fields.ToDictionary(f => f.Key, f => VariableNameRules.DefaultFor(kind, f)),
            Links = spec.Outputs.ToDictionary(output => output, _ => (Guid?)null)
        };
    }
}
