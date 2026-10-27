namespace KlikerBlocks;

public sealed class MouseMappingSettings
{
    public bool Enabled { get; set; }
    public List<MouseMappingProfile> Profiles { get; set; } = [];
}

public sealed class MouseMappingProfile
{
    public string Name { get; set; } = "Новый профиль";
    public bool Enabled { get; set; } = true;
    public string MatchMode { get; set; } = "Активное окно";
    public string ProcessName { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public string WindowClass { get; set; } = "";
    public bool UseRegion { get; set; }
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; } = 200;
    public int RegionHeight { get; set; } = 200;
    public List<MouseMappingLayer> Layers { get; set; } = [new()];
}

public sealed class MouseMappingLayer
{
    public string Name { get; set; } = "Слой 1";
    public List<MouseMappingBinding> Bindings { get; set; } = [];
}

public sealed class MouseMappingBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = "Кнопка 4";
    public string Trigger { get; set; } = "Нажатие";
    public string ChordSource { get; set; } = "";
    public int DelayMs { get; set; } = 300;
    public string Action { get; set; } = "Клавиши";
    public string Value { get; set; } = "Ctrl+C";
    public bool BlockOriginal { get; set; } = true;
}

internal readonly record struct MouseMappingWindow(string ProcessName, string Title, string ClassName,
    int CursorX, int CursorY);

internal readonly record struct MouseMappingContext(MouseMappingWindow Foreground, MouseMappingWindow Hovered);

internal static class MouseProfileMatcher
{
    public static int Select(MouseMappingSettings settings, MouseMappingContext context)
    {
        var fallback = -1;
        for (var i = 0; i < settings.Profiles.Count; i++)
        {
            var profile = settings.Profiles[i];
            if (!profile.Enabled) continue;
            if (profile.MatchMode == "Все окна") { if (fallback < 0) fallback = i; continue; }
            var window = profile.MatchMode == "Под курсором" ? context.Hovered : context.Foreground;
            if (window.ProcessName.Length == 0) continue;
            if (profile.ProcessName.Length > 0 &&
                !NormalizeProcess(window.ProcessName).Equals(NormalizeProcess(profile.ProcessName), StringComparison.OrdinalIgnoreCase)) continue;
            if (profile.TitleContains.Length > 0 &&
                !window.Title.Contains(profile.TitleContains, StringComparison.OrdinalIgnoreCase)) continue;
            if (profile.WindowClass.Length > 0 &&
                !window.ClassName.Equals(profile.WindowClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (profile.UseRegion && (profile.RegionWidth <= 0 || profile.RegionHeight <= 0 ||
                window.CursorX < profile.RegionX || window.CursorY < profile.RegionY ||
                (long)window.CursorX >= (long)profile.RegionX + profile.RegionWidth ||
                (long)window.CursorY >= (long)profile.RegionY + profile.RegionHeight)) continue;
            return i;
        }
        return fallback;
    }

    private static string NormalizeProcess(string name) =>
        name.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name.Trim()[..^4] : name.Trim();
}

internal static class MouseMappingNames
{
    public static readonly string[] Sources = ["ЛКМ", "ПКМ", "СКМ", "Кнопка 4", "Кнопка 5",
        "Колесо вверх", "Колесо вниз", "Наклон влево", "Наклон вправо"];
    public static readonly string[] Triggers = ["Нажатие", "Отпускание", "Удержание", "Повтор",
        "Переключатель", "Сочетание", "Короткое нажатие", "Долгое удержание"];
    public static readonly string[] Actions = ["Клавиши", "Кнопка мыши", "Запустить сценарий",
        "Остановить сценарий", "Переключить слой", "Слой пока удерживается", "Отключить"];
    public static bool IsWheel(string source) => source.StartsWith("Колесо", StringComparison.Ordinal) ||
        source.StartsWith("Наклон", StringComparison.Ordinal);
}
