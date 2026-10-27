using System.Text.Json;
using System.Globalization;

namespace KlikerBlocks;

internal static class UiLanguage
{
    public static readonly (string Code, string Name)[] Available =
    [
        ("ru", "Русский"), ("en", "English")
    ];
    private static Dictionary<string, string> _translations = new(StringComparer.Ordinal);
    public static string Code { get; private set; } = "en";

    public static void Configure(string code)
    {
        if (!Available.Any(language => language.Code == code)) code = "en";
        Code = code;
        _translations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (code == "ru") return;
        var path = Path.Combine(AppContext.BaseDirectory, "Languages", code + ".json");
        try
        {
            _translations = ReadPack(path, code);
            var extra = Path.Combine(AppContext.BaseDirectory, "Languages", code + "-extra.json");
            if (File.Exists(extra))
                foreach (var (key, value) in ReadPack(extra, code)) _translations[key] = value;
            foreach (var (key, value) in BlockUiTranslations.For(code))
                _translations.TryAdd(key, value);
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
        {
            Code = "ru";
            _translations.Clear();
        }
    }

    private static Dictionary<string, string> ReadPack(string path, string code)
    {
        var pack = JsonSerializer.Deserialize<Pack>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (pack?.Language != code || pack.Translations is null)
            throw new InvalidOperationException("Неверный код языкового пакета: " + path);
        return pack.Translations;
    }

    public static string T(string? original) => Code == "ru" ? original ?? "" :
        _translations.TryGetValue(original ?? "", out var localized) && !string.IsNullOrWhiteSpace(localized)
            ? localized : original ?? "";

    public static bool Has(string original) => Code == "ru" ||
        _translations.TryGetValue(original, out var text) && !string.IsNullOrWhiteSpace(text);

    public static string TOr(string original, string fallbackKey) => Has(original) ? T(original) : T(fallbackKey);

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);

    public static void ApplyToOpenForms()
    {
        if (Code == "ru") return;
        foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) Apply(form);
    }

    public static void ApplyTo(Control control)
    {
        if (Code != "ru") Apply(control);
    }

    private static void Apply(Control control)
    {
        if (control is not (TextBoxBase or ComboBox or ListBox or NumericUpDown))
            control.Text = T(control.Text);
        if (control is Form form && form.MainMenuStrip is { } menu)
            foreach (ToolStripItem item in menu.Items) Apply(item);
        if (control is ToolStrip strip)
            foreach (ToolStripItem item in strip.Items) Apply(item);
        foreach (Control child in control.Controls) Apply(child);
    }

    private static void Apply(ToolStripItem item)
    {
        item.Text = T(item.Text);
        if (item is ToolStripDropDownItem dropDown)
            foreach (ToolStripItem child in dropDown.DropDownItems) Apply(child);
    }

    private sealed class Pack
    {
        public string? Language { get; set; }
        public Dictionary<string, string>? Translations { get; set; }
    }
}
